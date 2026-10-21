using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public sealed class MythDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int Price { get; private set; }
        public IReadOnlyList<string> CompanionIds { get; private set; }
        internal MythDefinition(string id, string name, string description, int price, params string[] companions)
        { Id = id; Name = name; Description = description; Price = price; CompanionIds = Array.AsReadOnly(companions); }
    }

    public static class TowerMythCatalog
    {
        public const string StarterMythId = "eden";
        public const int MythPrice = 5;
        public static readonly IReadOnlyList<MythDefinition> Definitions = Array.AsReadOnly(new[]
        {
            new MythDefinition("eden", "에덴의 천사", "빛과 치유, 성스러운 검을 다루는 다섯 천사.", 0, "michael", "gabriel", "raphael", "uriel", "metatron"),
            new MythDefinition("olympus", "올림포스", "번개와 바다, 명계와 지혜, 달의 권능.", MythPrice, "zeus", "poseidon", "hades", "athena", "artemis"),
            new MythDefinition("asgard", "아스가르드", "룬과 천둥, 환술과 전장의 서약.", MythPrice, "odin", "thor", "loki", "freyja", "tyr"),
            new MythDefinition("india", "인도 신화", "창조와 수호, 파괴와 용기를 잇는 신과 영웅.", MythPrice, "shiva", "vishnu", "brahma", "ganesha", "hanuman"),
            new MythDefinition("egypt", "이집트 신화", "태양과 사후 세계를 수호하는 다섯 신.", MythPrice, "ra", "osiris", "isis", "anubis", "horus"),
            new MythDefinition("china", "중국 신화", "여의봉과 풍화륜, 신궁과 청룡언월도.", MythPrice, "sun_wukong", "nezha", "erlang_shen", "guan_yu", "nuwa"),
            new MythDefinition("korea", "한국 신화", "건국의 영웅과 하늘, 생명과 풍요의 수호자.", MythPrice, "dangun", "jumong", "hwanung", "princess_bari", "jacheongbi"),
            new MythDefinition("solomon", "솔로몬의 72악마", "왕관과 인장으로 힘을 제어하는 다섯 악마 군주.", MythPrice, "bael", "agares", "vassago", "paimon", "asmodeus"),
            new MythDefinition("ireland", "아일랜드 신화", "게이 볼그와 피아나, 광명과 전장의 전승.", MythPrice, "cu_chulainn", "fionn", "lugh", "morrigan", "dagda"),
            new MythDefinition("zodiac", "12간지", "자·축·인·묘·진을 인간형 수호 전사로 재해석.", MythPrice, "zodiac_rat", "zodiac_ox", "zodiac_tiger", "zodiac_rabbit", "zodiac_dragon")
        });
        public static MythDefinition Get(string id)
        { foreach (var myth in Definitions) if (myth.Id == id) return myth; return null; }
    }
}
