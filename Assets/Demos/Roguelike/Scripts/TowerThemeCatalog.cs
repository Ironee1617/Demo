using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public enum TowerThemeId { Lava, Rock, Jungle, Frozen, Water, Cloud, Ruins }

    public sealed class TowerMonsterDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Kind { get; private set; }
        public int Variant { get; private set; }
        public float HealthMultiplier { get; private set; }
        public float DamageMultiplier { get; private set; }
        public float MoveMultiplier { get; private set; }
        public float AttackRateMultiplier { get; private set; }
        public float BulletMultiplier { get; private set; }
        internal TowerMonsterDefinition(string id, string name, int kind, int variant,
            float health, float damage, float move, float rate, float bullet)
        {
            Id = id; Name = name; Kind = kind; Variant = variant;
            HealthMultiplier = health; DamageMultiplier = damage; MoveMultiplier = move;
            AttackRateMultiplier = rate; BulletMultiplier = bullet;
        }
    }

    public sealed class TowerThemeDefinition
    {
        public TowerThemeId Id { get; private set; }
        public string Name { get; private set; }
        public string AttackHex { get; private set; }
        public IReadOnlyList<TowerMonsterDefinition> Monsters { get; private set; }
        public TowerMonsterDefinition Boss { get; private set; }
        public int AirborneVariant
        { get { return Id == TowerThemeId.Frozen || Id == TowerThemeId.Water ? 4 : Id == TowerThemeId.Ruins ? 2 : 1; } }
        internal TowerThemeDefinition(TowerThemeId id, string name, string attack,
            TowerMonsterDefinition[] monsters, TowerMonsterDefinition boss)
        { Id = id; Name = name; AttackHex = attack; Monsters = Array.AsReadOnly(monsters); Boss = boss; }
    }

    public static class TowerThemeCatalog
    {
        public const int FloorsPerTheme = 5;
        public static readonly IReadOnlyList<TowerThemeDefinition> All = Array.AsReadOnly(new[] {
            Theme(TowerThemeId.Lava, "용암지대", "FF713F", "lava",
                new[] { "불씨 슬라임", "잿불 임프", "현무암 골렘", "화염 도마뱀", "용암 사냥개", "흑요석 술사" }, "용암 거신", 1.08f, 1.12f, .9f, 1f, 1f),
            Theme(TowerThemeId.Rock, "암석지대", "D5B58A", "rock",
                new[] { "조약돌 벌레", "수정 딱정벌레", "암석 파수병", "동굴 전갈", "수정 주술사", "철갑 트롤" }, "산맥의 거인", 1.22f, 1.08f, .8f, .9f, .95f),
            Theme(TowerThemeId.Jungle, "정글지대", "B6E764", "jungle",
                new[] { "이끼 슬라임", "가시 사마귀", "덩굴 수호자", "맹독 개구리", "정글 표범", "포자 주술사" }, "태고의 나무정령", 1.12f, 1f, .95f, 1.08f, 1f),
            Theme(TowerThemeId.Frozen, "빙결지대", "80DDFF", "frozen",
                new[] { "눈꽃 슬라임", "서리 늑대", "얼음 파수병", "고드름 거미", "눈보라 주술사", "빙하 설인" }, "빙결의 서리룡", 1.08f, 1.05f, 1f, .95f, 1.08f),
            Theme(TowerThemeId.Water, "수상지대", "62DBD0", "water",
                new[] { "물결 슬라임", "껍질 게", "산호 수호병", "작살 인어", "번개 해파리", "심해 술사" }, "심연의 크라켄", 1.15f, 1f, .85f, 1f, 1.05f),
            Theme(TowerThemeId.Cloud, "구름지대", "E9BAFF", "cloud",
                new[] { "안개 슬라임", "폭풍새", "구름 산양", "바람 정령", "번개 기사", "하늘 술사" }, "폭풍의 거조", .95f, 1f, 1.15f, 1.1f, 1.1f),
            Theme(TowerThemeId.Ruins, "폐허지대", "E8BA72", "ruins",
                new[] { "사막 쇠똥구리", "부서진 파수병", "무덤 망령", "해골 궁수", "저주받은 석상", "폐허 사제" }, "몰락한 파라오", 1f, 1.1f, 1f, 1.05f, 1f)
        });

        private static TowerThemeDefinition Theme(TowerThemeId id, string name, string attack, string prefix,
            string[] names, string boss, float hp, float damage, float move, float rate, float bullet)
        {
            // Melee, ranged and heavy enemies remain reachable without a ranged companion.
            int[] kinds = { 0, 1, 2, 0, 1, 2 };
            float[] health = { .85f, .9f, 1.15f, .8f, .95f, 1.05f };
            float[] power = { .9f, .9f, 1.1f, .85f, 1f, 1.05f };
            float[] speed = { .95f, 1.1f, .85f, 1.2f, 1.05f, .9f };
            float[] cadence = { 1f, 1.05f, .9f, 1.1f, 1f, .95f };
            var monsters = new TowerMonsterDefinition[names.Length];
            for (int i = 0; i < monsters.Length; i++)
                monsters[i] = new TowerMonsterDefinition(prefix + "_" + i, names[i], kinds[i], i,
                    health[i], power[i], speed[i], cadence[i], 1f);
            return new TowerThemeDefinition(id, name, attack, monsters,
                new TowerMonsterDefinition(prefix + "_boss", boss, 4, 0, hp, damage, move, rate, bullet));
        }

        public static TowerThemeDefinition Get(TowerThemeId id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            throw new ArgumentOutOfRangeException("id");
        }
        public static int BlockForFloor(int floor)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException("floor");
            return (floor - 1) / FloorsPerTheme;
        }
        public static TowerThemeDefinition Roll(Random random, TowerThemeId? previous = null)
        {
            if (random == null) throw new ArgumentNullException("random");
            int excluded = -1;
            for (int i = 0; i < All.Count; i++) if (previous.HasValue && All[i].Id == previous.Value) excluded = i;
            int pick = random.Next(All.Count - (excluded < 0 ? 0 : 1));
            if (excluded >= 0 && pick >= excluded) pick++;
            return All[pick];
        }
    }
}
