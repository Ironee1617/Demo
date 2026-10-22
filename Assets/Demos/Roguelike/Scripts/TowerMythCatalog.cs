using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public sealed class MythDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public IReadOnlyList<string> CompanionIds { get; private set; }
        internal MythDefinition(string id, string name, string description, params string[] companions)
        { Id = id; Name = name; Description = description; CompanionIds = Array.AsReadOnly(companions); }
    }

    public enum SynergyTarget { Members, Player, Party }

    public struct SynergyBonus
    {
        public SynergyTarget Target { get; private set; }
        public HeroStat Stat { get; private set; }
        public float Value { get; private set; }
        public SynergyBonus(SynergyTarget target, HeroStat stat, float value) : this() { Target = target; Stat = stat; Value = value; }
        public string Describe()
        {
            string target = Target == SynergyTarget.Members ? "소속 동료" : Target == SynergyTarget.Player ? "플레이어" : "파티 전체";
            return target + " " + AugmentDefinition.StatName(Stat) + " " + AugmentDefinition.FormatValue(Stat, Value);
        }
    }

    // Two or three companions of one mythology unlock its shared ability. Three replaces two.
    public sealed class MythSynergyDefinition
    {
        private static readonly SynergyBonus[] None = new SynergyBonus[0];
        private readonly SynergyBonus[] two, three;
        public string MythId { get; private set; }
        public string Name { get; private set; }
        internal MythSynergyDefinition(string mythId, string name, SynergyBonus[] two, SynergyBonus[] three)
        { MythId = mythId; Name = name; this.two = two; this.three = three; }
        public IReadOnlyList<SynergyBonus> Bonuses(int members)
        { return Array.AsReadOnly(members >= 3 ? three : members >= 2 ? two : None); }
        public string Describe(int members)
        {
            var parts = new List<string>();
            foreach (var bonus in Bonuses(members)) parts.Add(bonus.Describe());
            return String.Join(" · ", parts.ToArray());
        }
    }

    public sealed class MythSynergyState
    {
        public MythSynergyDefinition Definition { get; private set; }
        public int Members { get; private set; }
        public int Level { get { return Members >= 3 ? 3 : 2; } }
        public string Description { get { return Definition.Describe(Members); } }
        internal MythSynergyState(MythSynergyDefinition definition, int members) { Definition = definition; Members = members; }
    }

    public static class TowerMythCatalog
    {
        public static readonly IReadOnlyList<MythDefinition> Definitions = Array.AsReadOnly(new[]
        {
            new MythDefinition("eden", "에덴의 천사", "빛과 치유, 성스러운 검을 다루는 다섯 천사.", "michael", "gabriel", "raphael", "uriel", "metatron"),
            new MythDefinition("olympus", "올림포스", "번개와 바다, 명계와 지혜, 달의 권능.", "zeus", "poseidon", "hades", "athena", "artemis"),
            new MythDefinition("asgard", "아스가르드", "룬과 천둥, 환술과 전장의 서약.", "odin", "thor", "loki", "freyja", "tyr"),
            new MythDefinition("india", "인도 신화", "창조와 수호, 파괴와 용기를 잇는 신과 영웅.", "shiva", "vishnu", "brahma", "ganesha", "hanuman"),
            new MythDefinition("egypt", "이집트 신화", "태양과 사후 세계를 수호하는 다섯 신.", "ra", "osiris", "isis", "anubis", "horus"),
            new MythDefinition("china", "중국 신화", "여의봉과 풍화륜, 신궁과 청룡언월도.", "sun_wukong", "nezha", "erlang_shen", "guan_yu", "nuwa"),
            new MythDefinition("korea", "한국 신화", "건국의 영웅과 하늘, 생명과 풍요의 수호자.", "dangun", "jumong", "hwanung", "princess_bari", "jacheongbi"),
            new MythDefinition("solomon", "솔로몬의 72악마", "왕관과 인장으로 힘을 제어하는 다섯 악마 군주.", "bael", "agares", "vassago", "paimon", "asmodeus"),
            new MythDefinition("ireland", "아일랜드 신화", "게이 볼그와 피아나, 광명과 전장의 전승.", "cu_chulainn", "fionn", "lugh", "morrigan", "dagda"),
            new MythDefinition("zodiac", "12간지", "자·축·인·묘·진을 인간형 수호 전사로 재해석.", "zodiac_rat", "zodiac_ox", "zodiac_tiger", "zodiac_rabbit", "zodiac_dragon")
        });

        private static SynergyBonus Members(HeroStat stat, float value) { return new SynergyBonus(SynergyTarget.Members, stat, value); }
        private static SynergyBonus Player(HeroStat stat, float value) { return new SynergyBonus(SynergyTarget.Player, stat, value); }
        private static SynergyBonus Party(HeroStat stat, float value) { return new SynergyBonus(SynergyTarget.Party, stat, value); }

        public static readonly IReadOnlyList<MythSynergyDefinition> Synergies = Array.AsReadOnly(new[]
        {
            new MythSynergyDefinition("eden", "천상의 합창",
                new[] { Party(HeroStat.Healing, .25f), Player(HeroStat.Regeneration, 1.5f) },
                new[] { Party(HeroStat.Healing, .5f), Player(HeroStat.Regeneration, 3f) }),
            new MythSynergyDefinition("olympus", "신들의 권능",
                new[] { Members(HeroStat.SkillPower, .25f) },
                new[] { Members(HeroStat.SkillPower, .5f), Members(HeroStat.CritChance, .1f) }),
            new MythSynergyDefinition("asgard", "발할라의 함성",
                new[] { Party(HeroStat.Damage, .12f) },
                new[] { Party(HeroStat.Damage, .25f) }),
            new MythSynergyDefinition("india", "윤회의 수레바퀴",
                new[] { Members(HeroStat.Cooldown, .15f) },
                new[] { Members(HeroStat.Cooldown, .3f), Player(HeroStat.Cooldown, .1f) }),
            new MythSynergyDefinition("egypt", "태양선의 항해",
                new[] { Player(HeroStat.LifeSteal, .06f) },
                new[] { Player(HeroStat.LifeSteal, .12f), Player(HeroStat.MaxHealth, .1f) }),
            new MythSynergyDefinition("china", "천계의 무예",
                new[] { Party(HeroStat.AttackSpeed, .12f) },
                new[] { Party(HeroStat.AttackSpeed, .25f) }),
            new MythSynergyDefinition("korea", "홍익의 가호",
                new[] { Player(HeroStat.MaxHealth, .15f), Player(HeroStat.Armor, 4) },
                new[] { Player(HeroStat.MaxHealth, .3f), Player(HeroStat.Armor, 8) }),
            new MythSynergyDefinition("solomon", "72인장의 계약",
                new[] { Party(HeroStat.CritChance, .08f) },
                new[] { Party(HeroStat.CritChance, .16f), Members(HeroStat.SkillPower, .2f) }),
            new MythSynergyDefinition("ireland", "피아나의 사냥",
                new[] { Members(HeroStat.Range, .15f), Members(HeroStat.Projectiles, 1) },
                new[] { Members(HeroStat.Range, .3f), Members(HeroStat.Projectiles, 2) }),
            new MythSynergyDefinition("zodiac", "십이지 수호진",
                new[] { Player(HeroStat.MoveSpeed, .08f), Player(HeroStat.DodgeCooldown, .15f) },
                new[] { Player(HeroStat.MoveSpeed, .15f), Player(HeroStat.DodgeCooldown, .3f) })
        });

        public static MythDefinition Get(string id)
        { foreach (var myth in Definitions) if (myth.Id == id) return myth; return null; }
        public static MythSynergyDefinition GetSynergy(string mythId)
        { foreach (var synergy in Synergies) if (synergy.MythId == mythId) return synergy; return null; }
    }
}
