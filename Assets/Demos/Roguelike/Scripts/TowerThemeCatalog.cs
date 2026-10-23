using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// The tower's nine ten-floor chapters in climbing order, then the village hub that only dresses the
    /// world (it has no monsters). Terrain atlas columns and backdrop ids follow this order.
    /// </summary>
    public enum TowerThemeId { Olympus, Asgard, Dangun, Eden, Veda, Papyrus, Chinese, Hell, Abyss, Village }

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
        /// <summary>The evil god waiting on the tower's last floor; only the abyss chapter has one.</summary>
        public TowerMonsterDefinition FinalBoss { get; private set; }
        // The painted creature boards decide which species can hover: the frost and tide boards carry
        // their flyer in column four, the Egyptian board in column two, the rest in column one. The abyss
        // borrows the tomb wraith for its fourth slot.
        public int AirborneVariant
        { get { return Id == TowerThemeId.Asgard || Id == TowerThemeId.Veda || Id == TowerThemeId.Abyss ? 4 : Id == TowerThemeId.Papyrus ? 2 : 1; } }
        internal TowerThemeDefinition(TowerThemeId id, string name, string attack,
            TowerMonsterDefinition[] monsters, TowerMonsterDefinition boss, TowerMonsterDefinition finalBoss = null)
        { Id = id; Name = name; AttackHex = attack; Monsters = Array.AsReadOnly(monsters); Boss = boss; FinalBoss = finalBoss; }
    }

    public static class TowerThemeCatalog
    {
        public const int FloorsPerTheme = 10;
        /// <summary>Art slot of the final boss; regular species use 0-5 and the chapter boss slot 6.</summary>
        public const int FinalVariant = 7;
        // Each mythology keeps one of the seven painted creature boards (see TowerThemeMonsterArt.Sheet):
        // Olympus the storm-sky board, Asgard the frost board, Dangun the cavern board, Eden the garden
        // board, Veda the tide board, Papyrus the tomb board and the Chinese realm the ember board.
        // Hell regrades the ember board in blood and the abyss stitches creatures from several boards.
        public static readonly IReadOnlyList<TowerThemeDefinition> All = Array.AsReadOnly(new[] {
            Theme(TowerThemeId.Olympus, "올림포스", "E9BAFF", "olympus",
                new[] { "올림포스 안개령", "하르피이아", "황금 양모 숫양", "아네모이의 바람 정령", "뇌정 호플리테스", "델포이의 신관" }, "제우스의 독수리", .95f, 1f, 1.15f, 1.1f, 1.1f),
            Theme(TowerThemeId.Asgard, "아스가르드", "80DDFF", "asgard",
                new[] { "니플헤임 서리 슬라임", "펜리르의 후예", "발할라 서리 파수병", "요툰헤임 얼음 거미", "눈보라 볼바", "서리 거인 요툰" }, "니드호그", 1.08f, 1.05f, 1f, .95f, 1.08f),
            Theme(TowerThemeId.Dangun, "단군신화", "D5B58A", "dangun",
                new[] { "돌탑 벌레", "옥 딱정벌레", "장승 파수꾼", "동굴 전갈", "산중 무당", "태백산 곰 거인" }, "태백산 산신", 1.22f, 1.08f, .8f, .9f, .95f),
            Theme(TowerThemeId.Eden, "에덴", "B6E764", "eden",
                new[] { "낙원 이끼 슬라임", "가시 사마귀", "에덴의 덩굴 수호자", "유혹의 독개구리", "낙원의 표범", "포자 사제" }, "선악과 나무의 정령", 1.12f, 1f, .95f, 1.08f, 1f),
            Theme(TowerThemeId.Veda, "베다", "62DBD0", "veda",
                new[] { "강가의 물결 슬라임", "마카라 게", "나가 산호 수호병", "바루나의 작살병", "인드라의 뇌광 해파리", "나가 주술사" }, "브리트라", 1.15f, 1f, .85f, 1f, 1.05f),
            Theme(TowerThemeId.Papyrus, "파피루스", "E8BA72", "papyrus",
                new[] { "케프리의 풍뎅이", "부서진 메자이 파수병", "두아트의 망령", "미라 궁수", "저주받은 우샤브티", "세트의 사제" }, "불멸의 파라오", 1f, 1.1f, 1f, 1.05f, 1f),
            Theme(TowerThemeId.Chinese, "중국신화", "FF713F", "chinese",
                new[] { "화염산 불씨 슬라임", "잿불 요괴", "청동 종정 골렘", "화룡의 새끼", "불꽃 해태", "흑요석 도사" }, "축융의 화신", 1.08f, 1.12f, .9f, 1f, 1f),
            Theme(TowerThemeId.Hell, "지옥", "FF3B2E", "hell",
                new[] { "유황 덩어리", "지옥불 임프", "형벌의 바위 골렘", "불지옥 도마뱀", "지옥의 사냥개", "흑마법 사제" }, "타락한 우상 몰록", 1.18f, 1.14f, .95f, 1.05f, 1.05f),
            Theme(TowerThemeId.Abyss, "심연", "D11A4A", "abyss",
                new[] { "살갗 낫사마귀", "눈알 해파리", "살덩이 식인귀", "눈알 거미", "울부짖는 망령", "악신의 사도" }, "심연의 촉수 전령", 1.25f, 1.18f, .9f, 1.05f, 1.08f,
                new TowerMonsterDefinition("abyss_final", "멸세의 악신 니힐로스", 4, FinalVariant, 1.9f, 1.25f, .9f, 1.05f, 1.05f))
        });

        private static TowerThemeDefinition Theme(TowerThemeId id, string name, string attack, string prefix,
            string[] names, string boss, float hp, float damage, float move, float rate, float bullet, TowerMonsterDefinition finalBoss = null)
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
                new TowerMonsterDefinition(prefix + "_boss", boss, 4, 0, hp, damage, move, rate, bullet), finalBoss);
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
        /// <summary>The chapter theme of a floor; custom climbs past the ninetieth floor stay in the abyss.</summary>
        public static TowerThemeDefinition ForFloor(int floor)
        { return All[Math.Min(All.Count - 1, BlockForFloor(floor))]; }
    }
}
