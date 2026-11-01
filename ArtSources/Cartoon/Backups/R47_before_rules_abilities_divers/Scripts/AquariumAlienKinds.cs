using System;

namespace PrototypeLab.Aquarium
{
    // The playable aliens (2026-10-28). Every kind is the cute monster with its own head (antenna and eye
    // expression) and colours; all share the same rig, clips and rules. Each kind will get its own ability:
    // the ability fields below are the slot for it (shown in the lobby) and are not designed yet.
    // Kind ids are sent over the network (Swimmer.alienKind), so new kinds go at the end.
    [Serializable]
    public sealed class AquariumAlienKind
    {
        public int id;
        public string key, name, look, prefab;
        // The kind's own ability (not designed yet): a short name and one line for the lobby card.
        public string abilityName, abilityText;
        // Theme colour for the lobby and HUD (0..1).
        public float r, g, b;
    }

    public static class AquariumAlienKinds
    {
        public static readonly AquariumAlienKind[] All =
        {
            new AquariumAlienKind { id = 0, key = "Purple", name = "보라 외계인", look = "새싹 더듬이 · 동그란 눈", prefab = "AquariumCharacters/Alien",
                abilityName = "고유 능력 · 준비 중", abilityText = "보라 외계인만의 능력이 들어갈 자리입니다.", r = .74f, g = .55f, b = .95f },
            new AquariumAlienKind { id = 1, key = "Green", name = "초록 외계인", look = "공 더듬이 · 웃는 눈", prefab = "AquariumCharacters/Alien_Green",
                abilityName = "고유 능력 · 준비 중", abilityText = "초록 외계인만의 능력이 들어갈 자리입니다.", r = .47f, g = .84f, b = .42f },
            new AquariumAlienKind { id = 2, key = "Blue", name = "파랑 외계인", look = "초승달 더듬이 · 졸린 눈", prefab = "AquariumCharacters/Alien_Blue",
                abilityName = "고유 능력 · 준비 중", abilityText = "파랑 외계인만의 능력이 들어갈 자리입니다.", r = .42f, g = .63f, b = .98f },
            new AquariumAlienKind { id = 3, key = "Yellow", name = "노랑 외계인", look = "찌릿 더듬이 · 반쯤 뜬 눈", prefab = "AquariumCharacters/Alien_Yellow",
                abilityName = "고유 능력 · 준비 중", abilityText = "노랑 외계인만의 능력이 들어갈 자리입니다.", r = .98f, g = .76f, b = .2f },
            new AquariumAlienKind { id = 4, key = "Red", name = "빨강 외계인", look = "고깔 더듬이 · 시큰둥한 눈", prefab = "AquariumCharacters/Alien_Red",
                abilityName = "고유 능력 · 준비 중", abilityText = "빨강 외계인만의 능력이 들어갈 자리입니다.", r = .95f, g = .36f, b = .33f },
        };
        // Lobby order, left to right: the original first, then the variants in the order they were made.
        public static readonly int[] LobbyOrder = { 0, 1, 2, 3, 4 };
        public static int Count { get { return All.Length; } }
        public static int Clamp(int kind) { return kind < 0 || kind >= All.Length ? 0 : kind; }
        public static AquariumAlienKind Get(int kind) { return All[Clamp(kind)]; }
        // AI aliens spread over the kinds so a tank shows every alien.
        public static int ForBot(int id) { return ((id % All.Length) + All.Length) % All.Length; }
    }
}
