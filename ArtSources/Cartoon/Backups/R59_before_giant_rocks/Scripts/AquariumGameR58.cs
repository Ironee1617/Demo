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

        // The chip in the left card: picture, name, and the way to the map tab.
        private void MapChip(float x, float y, float width)
        {
            WatchHostMap();
            int map = session.Map; var rect = new Rect(x, y, width, 42);
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition), open = lobbyTab == MapTab;
            Shape(rect, open ? new Color(0.12f, 0.3f, 0.38f, 0.92f) : hover ? new Color(0.13f, 0.32f, 0.4f, 0.92f) : new Color(0.06f, 0.19f, 0.26f, 0.86f), 10);
            Shape(rect, open || hover ? new Color(cyan.r, cyan.g, cyan.b, .8f) : Rim, 10, 1.2f);
            var thumb = MapThumb(map); var picture = new Rect(x + 5, y + 5, 52, 32);
            if (thumb != null) GUI.DrawTexture(picture, thumb, ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, 6);
            else Shape(picture, new Color(0.1f, 0.3f, 0.36f, 1), 6);
            Label(x + 66, y + 3, width - 140, 18, session.IsHost ? "다음 게임 맵" : "방장이 고른 맵", caption);
            Label(x + 66, y + 18, width - 140, 22, AquariumMaps.Name(map), Tinted(small, Color.white));
            Label(x + width - 74, y + 11, 66, 22, session.IsHost ? "맵 선택 ›" : "보기 ›", Tinted(small, cyan));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) lobbyTab = open ? 0 : MapTab;
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
            float cardWidth = (width - 48 - gap) / 2, top = y + 92, cardHeight = height - 92 - 66;
            var e = Event.current;
            for (int map = 0; map < AquariumMaps.Count; map++)
            {
                var card = new Rect(x + 24 + map * (cardWidth + gap), top, cardWidth, cardHeight);
                bool chosen = map == current, hover = CanPickMap && !chosen && card.Contains(e.mousePosition);
                Shape(card, chosen ? new Color(0.1f, 0.28f, 0.35f, 0.9f) : hover ? new Color(0.09f, 0.24f, 0.31f, 0.85f) : new Color(0.05f, 0.15f, 0.21f, 0.75f), 14);
                Shape(card, chosen ? cyan : hover ? new Color(cyan.r, cyan.g, cyan.b, .6f) : Rim, 14, chosen ? 2.4f : 1.2f);
                var picture = new Rect(card.x + 12, card.y + 12, card.width - 24, (card.width - 24) * .625f);
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
                Label(card.x + 16, ty + 36, card.width - 32, 24, AquariumMaps.Short(map) + ",  " + AquariumMaps.Size(map), body);
                Label(card.x + 16, ty + 64, card.width - 32, 44, AquariumMaps.Description(map), small);
                string[] features = map == AquariumMaps.Twin
                    ? new[] { "바위 아치, 탁자 바위, 처마 바위 아래에 숨기", "터널로 두 수조를 오가는 추격", "말미잘에 사는 흰동가리와 산호" }
                    : new[] { "거대한 바위 절벽과 천연 다리", "유리 산책로 아래 그늘에 숨기", "판마다 절벽 방향과 수초 배치 변화" };
                for (int i = 0; i < features.Length; i++)
                {
                    float fy = ty + 112 + i * 24;
                    Shape(new Rect(card.x + 18, fy + 9, 6, 6), chosen ? cyan : muted, 3);
                    Label(card.x + 32, fy, card.width - 48, 24, features[i], small);
                }
                var action = new Rect(card.x + 12, card.yMax - 54, card.width - 24, 42);
                if (chosen) { Shape(action, new Color(cyan.r, cyan.g, cyan.b, .16f), 10); GUI.Label(action, host ? "다음 게임에서 이 맵을 씁니다" : "방장이 고른 맵입니다", Tinted(smallCentred, cyan)); }
                else if (CanPickMap) { if (Button(action, "이 맵으로 하기")) PickMap(map); }
                else { Shape(action, new Color(0.04f, 0.12f, 0.17f, .6f), 10); GUI.Label(action, "방장만 바꿀 수 있습니다", Tinted(smallCentred, muted)); }
                if (CanPickMap && !chosen && GUI.Button(new Rect(card.x, card.y, card.width, action.y - card.y), GUIContent.none, GUIStyle.none)) PickMap(map);
            }
            Label(x + 24, y + height - 50, width - 48, 40, host ? "쌍둥이 수조는 판마다 바위, 산호, 말미잘, 배수구 자리가 새로 바뀝니다."
                : "보기 전용: 방장이 맵을 바꾸면 여기에 바로 보입니다.", small);
        }
    }
}
