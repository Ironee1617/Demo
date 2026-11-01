using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public static class TowerAugmentCatalog
    {
        public static readonly IReadOnlyList<CompanionDefinition> Companions = Array.AsReadOnly(new[]
        {
            C("eden", "michael", "미카엘", "천군의 검", RewardTier.Silver, CompanionRole.Vanguard, 100, 18, .95f, 2.3f, 0, "F6D574"),
            C("eden", "gabriel", "가브리엘", "계시의 전령", RewardTier.Silver, CompanionRole.Archer, 101, 12, 1.05f, 8.2f, 0, "E7EDF7"),
            C("eden", "raphael", "라파엘", "치유의 천사", RewardTier.Gold, CompanionRole.Healer, 102, 12, 1.25f, 7, 8, "9BE6BF"),
            C("eden", "uriel", "우리엘", "신의 불꽃", RewardTier.Gold, CompanionRole.Mage, 103, 20, 1.35f, 7.8f, 0, "F4AA72"),
            C("eden", "metatron", "메타트론", "천상의 서기관", RewardTier.Diamond, CompanionRole.Mage, 104, 26, 1.4f, 8.4f, 0, "D1B7FF"),
            C("olympus", "zeus", "제우스", "번개의 군주", RewardTier.Silver, CompanionRole.Mage, 105, 15, 1.2f, 7.5f, 0, "F4D46B"),
            C("olympus", "poseidon", "포세이돈", "바다의 왕", RewardTier.Silver, CompanionRole.Vanguard, 106, 18, 1.15f, 2.8f, 0, "70CDE5"),
            C("olympus", "hades", "하데스", "명계의 지배자", RewardTier.Gold, CompanionRole.Mage, 107, 21, 1.35f, 7.6f, 0, "B4A2D9"),
            C("olympus", "athena", "아테나", "지혜의 수호자", RewardTier.Gold, CompanionRole.Vanguard, 108, 22, 1.05f, 2.6f, 0, "BBD5E4"),
            C("olympus", "artemis", "아르테미스", "달빛의 사냥꾼", RewardTier.Diamond, CompanionRole.Archer, 109, 21, .85f, 9, 0, "B7DFBB"),
            C("asgard", "odin", "오딘", "외눈의 현자", RewardTier.Silver, CompanionRole.Mage, 110, 16, 1.3f, 8, 0, "A9BDE8"),
            C("asgard", "thor", "토르", "천둥의 수호자", RewardTier.Silver, CompanionRole.Vanguard, 111, 21, 1.3f, 2.4f, 0, "E4B282"),
            C("asgard", "loki", "로키", "환술의 달인", RewardTier.Gold, CompanionRole.Mage, 112, 16, .9f, 7, 0, "88D2AC"),
            C("asgard", "freyja", "프레이야", "황금의 여주인", RewardTier.Gold, CompanionRole.Healer, 113, 14, 1.2f, 7.5f, 7, "E4B5E7"),
            C("asgard", "tyr", "티르", "맹세의 전신", RewardTier.Diamond, CompanionRole.Vanguard, 114, 29, 1.05f, 2.5f, 0, "D4C9AA"),
            C("india", "shiva", "시바", "우주의 무희", RewardTier.Silver, CompanionRole.Vanguard, 115, 17, .95f, 2.5f, 0, "99BEEA"),
            C("india", "vishnu", "비슈누", "세계의 수호자", RewardTier.Silver, CompanionRole.Archer, 116, 13, 1.1f, 8.1f, 0, "86C8DC"),
            C("india", "brahma", "브라흐마", "창조의 현자", RewardTier.Gold, CompanionRole.Mage, 117, 22, 1.5f, 8.3f, 0, "E5B89A"),
            C("india", "ganesha", "가네샤", "장애물을 여는 자", RewardTier.Gold, CompanionRole.Healer, 118, 13, 1.4f, 7.2f, 9, "E9BC85"),
            C("india", "hanuman", "하누만", "바람의 영웅", RewardTier.Diamond, CompanionRole.Vanguard, 119, 24, .85f, 2.4f, 0, "F2C58D"),
            C("egypt", "ra", "라", "태양의 항해자", RewardTier.Silver, CompanionRole.Mage, 120, 17, 1.35f, 7.8f, 0, "F3CF67"),
            C("egypt", "osiris", "오시리스", "부활의 왕", RewardTier.Silver, CompanionRole.Healer, 121, 10, 1.4f, 6.8f, 5, "89D7B3"),
            C("egypt", "isis", "이시스", "마법의 여왕", RewardTier.Gold, CompanionRole.Healer, 122, 14, 1.25f, 7.6f, 8, "A6DDED"),
            C("egypt", "anubis", "아누비스", "저울의 수호자", RewardTier.Gold, CompanionRole.Vanguard, 123, 23, 1.1f, 2.6f, 0, "B5A2D1"),
            C("egypt", "horus", "호루스", "하늘의 왕", RewardTier.Diamond, CompanionRole.Archer, 124, 24, 1.05f, 9.2f, 0, "DAB878"),
            C("china", "sun_wukong", "손오공", "제천대성", RewardTier.Silver, CompanionRole.Vanguard, 125, 17, 1, 3.1f, 0, "E5BE75"),
            C("china", "nezha", "나타", "연꽃의 소년 영웅", RewardTier.Silver, CompanionRole.Vanguard, 126, 13, .75f, 2.2f, 0, "F29592"),
            C("china", "erlang_shen", "이랑신", "삼안의 수호신", RewardTier.Gold, CompanionRole.Archer, 127, 21, 1.2f, 9, 0, "ACCBE7"),
            C("china", "guan_yu", "관우", "충의의 무신", RewardTier.Gold, CompanionRole.Vanguard, 128, 27, 1.3f, 3, 0, "8DCAA2"),
            C("china", "nuwa", "여와", "하늘을 기운 창조자", RewardTier.Diamond, CompanionRole.Healer, 129, 17, 1.3f, 8, 10, "D9A8E3"),
            C("korea", "dangun", "단군", "고조선의 시조", RewardTier.Silver, CompanionRole.Vanguard, 130, 19, 1.1f, 2.5f, 0, "D5BF85"),
            C("korea", "jumong", "주몽", "고구려의 신궁", RewardTier.Silver, CompanionRole.Archer, 131, 14, 1.15f, 8.8f, 0, "86BED8"),
            C("korea", "hwanung", "환웅", "신시의 주인", RewardTier.Gold, CompanionRole.Mage, 132, 20, 1.25f, 8.1f, 0, "D6DDEE"),
            C("korea", "princess_bari", "바리공주", "망자를 이끄는 자", RewardTier.Gold, CompanionRole.Healer, 133, 11, 1.35f, 7.4f, 10, "A0DECC"),
            C("korea", "jacheongbi", "자청비", "곡식의 여신", RewardTier.Diamond, CompanionRole.Archer, 134, 22, .95f, 8.5f, 0, "E5D48B"),
            C("solomon", "bael", "바엘", "첫 번째 왕", RewardTier.Silver, CompanionRole.Mage, 135, 14, 1.05f, 7.2f, 0, "B39ACE"),
            C("solomon", "agares", "아가레스", "언어의 공작", RewardTier.Silver, CompanionRole.Mage, 136, 18, 1.5f, 8, 0, "B0CA8A"),
            C("solomon", "vassago", "바사고", "잃어버린 것의 예언자", RewardTier.Gold, CompanionRole.Archer, 137, 18, .95f, 8.8f, 0, "9CBFE8"),
            C("solomon", "paimon", "파이몬", "지식의 왕", RewardTier.Gold, CompanionRole.Mage, 138, 23, 1.5f, 8.4f, 0, "E3C17B"),
            C("solomon", "asmodeus", "아스모데우스", "불꽃의 군주", RewardTier.Diamond, CompanionRole.Vanguard, 139, 30, 1.15f, 2.7f, 0, "E196A3"),
            C("ireland", "cu_chulainn", "쿠 훌린", "울라의 사냥개", RewardTier.Silver, CompanionRole.Vanguard, 140, 16, .9f, 2.7f, 0, "B9BAE0"),
            C("ireland", "fionn", "핀 막 쿨", "피아나의 대장", RewardTier.Silver, CompanionRole.Archer, 141, 13, 1.05f, 8.2f, 0, "A2CCA0"),
            C("ireland", "lugh", "루", "긴 팔의 광명신", RewardTier.Gold, CompanionRole.Archer, 142, 21, 1.1f, 9.1f, 0, "EBD187"),
            C("ireland", "morrigan", "모리안", "전장의 예언자", RewardTier.Gold, CompanionRole.Mage, 143, 19, 1.15f, 7.8f, 0, "BA9CCF"),
            C("ireland", "dagda", "다그다", "풍요의 아버지", RewardTier.Diamond, CompanionRole.Healer, 144, 18, 1.4f, 7.6f, 11, "C7AF83"),
            C("zodiac", "zodiac_rat", "자 · 쥐 수호자", "지혜의 척후병", RewardTier.Silver, CompanionRole.Vanguard, 145, 12, .7f, 1.9f, 0, "AFBBD7"),
            C("zodiac", "zodiac_ox", "축 · 소 수호자", "인내의 거신", RewardTier.Silver, CompanionRole.Vanguard, 146, 23, 1.5f, 2.8f, 0, "C9A883"),
            C("zodiac", "zodiac_tiger", "인 · 호랑이 수호자", "용맹의 무사", RewardTier.Gold, CompanionRole.Vanguard, 147, 22, .95f, 2.4f, 0, "EDBB7E"),
            C("zodiac", "zodiac_rabbit", "묘 · 토끼 수호자", "달의 약사", RewardTier.Gold, CompanionRole.Healer, 148, 12, 1.15f, 7.4f, 8, "E8B8D4"),
            C("zodiac", "zodiac_dragon", "진 · 용 수호자", "구름의 황제", RewardTier.Diamond, CompanionRole.Mage, 149, 27, 1.45f, 8.6f, 0, "96D8CA")
        });

        public static readonly IReadOnlyList<AugmentDefinition> Augments = Build();

        // The innate trait is the companion's passive; its base numbers follow in the party panel.
        private static CompanionDefinition C(string mythId, string id, string name, string title, RewardTier tier,
            CompanionRole role, int art, float damage, float interval, float range, float healing, string tint)
        {
            string stats = "공격력 " + AugmentDefinition.Number(damage) + " · 공격 간격 " + AugmentDefinition.Number(interval) + "초 · 사거리 " + AugmentDefinition.Number(range);
            if (healing > 0) stats += " · 5초마다 체력 " + AugmentDefinition.Number(healing) + " 회복";
            string attack = role == CompanionRole.Vanguard ? "근접 무기로 공격합니다." : role == CompanionRole.Archer ? "원거리 무기로 공격합니다." : "마력탄으로 공격합니다.";
            var passive = TowerPassiveCatalog.For(id);
            return new CompanionDefinition(id, name, title, attack + "\n고유 패시브 · " + passive.Name + ": " + passive.Description, tier, role, art, damage, interval, range, healing, tint, mythId, passive.Name, stats);
        }

        private static IReadOnlyList<AugmentDefinition> Build()
        {
            var result = new List<AugmentDefinition>();
            Skills(result, "michael", "천군의 검무|불멸의 성벽|성검의 파동|낙원의 돌격|천군의 군기|불꽃 검의 원환", SkillEffect.BladeStorm, SkillEffect.ShieldPulse, SkillEffect.Shockwave, SkillEffect.DashStrike, SkillEffect.WarBanner, SkillEffect.Orbit);
            Skills(result, "gabriel", "계시의 화살|복음의 산광|일곱 나팔의 비|천상의 울림|계시의 표식|나팔의 메아리탄", SkillEffect.PierceShot, SkillEffect.MultiShot, SkillEffect.RainArrows, SkillEffect.ChainLightning, SkillEffect.HuntMark, SkillEffect.Ricochet);
            Skills(result, "raphael", "토비아의 기적|여행자의 가호|생명의 숨결|치유의 서리|에덴의 생명꽃|인도하는 빛구슬", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.LifeDrain, SkillEffect.IceNova, SkillEffect.Bloom, SkillEffect.Orbit);
            Skills(result, "uriel", "에덴의 화염|정화의 불안개|불꽃의 계시|심판의 낙화|심판의 화염 인장|정죄의 낙인", SkillEffect.Fireball, SkillEffect.PoisonCloud, SkillEffect.ArcaneVolley, SkillEffect.Meteor, SkillEffect.Mine, SkillEffect.Curse);
            Skills(result, "metatron", "천상 기록진|세피로트 연사|생명나무 결계|천구의 심판|천상 기록의 표식|메르카바의 궤도", SkillEffect.Sentry, SkillEffect.ArcaneVolley, SkillEffect.ShieldPulse, SkillEffect.Meteor, SkillEffect.HuntMark, SkillEffect.Orbit);
            Skills(result, "zeus", "올림포스의 벼락|뇌창의 산개|천둥의 포효|크로노스의 종언|뇌전 도탄|천공의 회오리", SkillEffect.ChainLightning, SkillEffect.MultiShot, SkillEffect.Shockwave, SkillEffect.Meteor, SkillEffect.Ricochet, SkillEffect.Vortex);
            Skills(result, "poseidon", "삼지창의 해일|얼어붙은 심해|파도의 창|대양의 격류|심해의 소용돌이|삼지창 낚아채기", SkillEffect.Shockwave, SkillEffect.IceNova, SkillEffect.PierceShot, SkillEffect.RainArrows, SkillEffect.Vortex, SkillEffect.Hook);
            Skills(result, "hades", "명계의 징수|아스포델 안개|저승의 윤무|지하세계의 문|명계의 낙인|저승의 사슬", SkillEffect.LifeDrain, SkillEffect.PoisonCloud, SkillEffect.BladeStorm, SkillEffect.Meteor, SkillEffect.Curse, SkillEffect.Hook);
            Skills(result, "athena", "아이기스 전개|팔라스의 돌격|지혜의 투창|승리의 전술|승리의 군기|아이기스 투척", SkillEffect.ShieldPulse, SkillEffect.DashStrike, SkillEffect.PierceShot, SkillEffect.Shockwave, SkillEffect.WarBanner, SkillEffect.Boomerang);
            Skills(result, "artemis", "초승달 산탄|은활의 추적|달의 화살비|사냥의 서리|사냥감의 표식|사냥꾼의 덫", SkillEffect.MultiShot, SkillEffect.PierceShot, SkillEffect.RainArrows, SkillEffect.IceNova, SkillEffect.HuntMark, SkillEffect.Mine);
            Skills(result, "odin", "궁니르 투척|룬의 사슬|후긴의 감시|라그나로크의 징조|외눈의 응시|룬의 저주", SkillEffect.PierceShot, SkillEffect.ChainLightning, SkillEffect.Sentry, SkillEffect.Meteor, SkillEffect.HuntMark, SkillEffect.Curse);
            Skills(result, "thor", "묠니르 강타|천둥의 귀환|폭풍 돌격|거인 분쇄|묠니르 귀환|대지 강타", SkillEffect.Shockwave, SkillEffect.ChainLightning, SkillEffect.DashStrike, SkillEffect.Meteor, SkillEffect.Boomerang, SkillEffect.Quake);
            Skills(result, "loki", "독사의 속삭임|거짓 그림자|환영의 연사|요툰의 냉기|거짓 분신|장난꾸러기의 덫", SkillEffect.PoisonCloud, SkillEffect.DashStrike, SkillEffect.ArcaneVolley, SkillEffect.IceNova, SkillEffect.Decoy, SkillEffect.Mine);
            Skills(result, "freyja", "폴크방의 자비|황금 목걸이 결계|세이드르의 맥동|발키리의 비|폴크방의 꽃씨|발키리의 깃발", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.LifeDrain, SkillEffect.RainArrows, SkillEffect.Bloom, SkillEffect.WarBanner);
            Skills(result, "tyr", "외팔의 검무|깨지지 않는 서약|펜리르 봉쇄|전신의 판결|글레이프니르 사슬|전쟁신의 군기", SkillEffect.BladeStorm, SkillEffect.ShieldPulse, SkillEffect.DashStrike, SkillEffect.Shockwave, SkillEffect.Hook, SkillEffect.WarBanner);
            Skills(result, "shiva", "탄다바 검무|다마루의 진동|제삼안의 불꽃|파괴와 재생|탄다바의 지진|삼지창 윤무", SkillEffect.BladeStorm, SkillEffect.Shockwave, SkillEffect.Fireball, SkillEffect.Meteor, SkillEffect.Quake, SkillEffect.Orbit);
            Skills(result, "vishnu", "수다르샤나 투척|보존의 결계|차크라의 분열|연꽃의 안식|차크라 귀환|연꽃 씨앗", SkillEffect.PierceShot, SkillEffect.ShieldPulse, SkillEffect.MultiShot, SkillEffect.HealPulse, SkillEffect.Boomerang, SkillEffect.Bloom);
            Skills(result, "brahma", "창조의 만다라|네 지혜의 빛|태초의 고요|우주의 개벽|네 얼굴의 구체|창조의 소용돌이", SkillEffect.Sentry, SkillEffect.ArcaneVolley, SkillEffect.IceNova, SkillEffect.Meteor, SkillEffect.Orbit, SkillEffect.Vortex);
            Skills(result, "ganesha", "장애물의 봉인|모다카의 축복|지혜의 진동|길잡이의 인장|길잡이 깃발|장애물의 덫", SkillEffect.ShieldPulse, SkillEffect.HealPulse, SkillEffect.Shockwave, SkillEffect.Sentry, SkillEffect.WarBanner, SkillEffect.Mine);
            Skills(result, "hanuman", "바람의 도약|금강 곤봉격|원숭이왕의 난무|랑카의 불꽃|산을 드는 일격|거대화 분신", SkillEffect.DashStrike, SkillEffect.Shockwave, SkillEffect.BladeStorm, SkillEffect.Fireball, SkillEffect.Quake, SkillEffect.Decoy);
            Skills(result, "ra", "태양선의 포화|태양 원반 연사|낮의 수호|정오의 심판|태양 원반 궤도|라의 눈", SkillEffect.Fireball, SkillEffect.ArcaneVolley, SkillEffect.ShieldPulse, SkillEffect.Meteor, SkillEffect.Orbit, SkillEffect.HuntMark);
            Skills(result, "osiris", "부활의 씨앗|두아트의 순환|왕권의 보호|나일의 녹무|부활의 꽃씨|미라의 저주", SkillEffect.HealPulse, SkillEffect.LifeDrain, SkillEffect.ShieldPulse, SkillEffect.PoisonCloud, SkillEffect.Bloom, SkillEffect.Curse);
            Skills(result, "isis", "왕좌의 날개|생명의 매듭|헤카의 연사|별빛의 장막|날개의 군기|마법 이름의 구체", SkillEffect.ShieldPulse, SkillEffect.HealPulse, SkillEffect.ArcaneVolley, SkillEffect.IceNova, SkillEffect.WarBanner, SkillEffect.Orbit);
            Skills(result, "anubis", "심장의 저울|저승길 돌진|방부의 검무|진실의 판결|저승의 갈고리|심장 저울의 저주", SkillEffect.LifeDrain, SkillEffect.DashStrike, SkillEffect.BladeStorm, SkillEffect.Shockwave, SkillEffect.Hook, SkillEffect.Curse);
            Skills(result, "horus", "왕의 눈|매의 산개|하늘왕의 비|복수의 섬광|호루스의 눈|매의 도탄", SkillEffect.PierceShot, SkillEffect.MultiShot, SkillEffect.RainArrows, SkillEffect.ChainLightning, SkillEffect.HuntMark, SkillEffect.Ricochet);
            Skills(result, "sun_wukong", "여의봉 난무|근두운 돌격|대성의 일격|털끝 분신진|칠십이변 분신|여의봉 귀환", SkillEffect.BladeStorm, SkillEffect.DashStrike, SkillEffect.Shockwave, SkillEffect.Sentry, SkillEffect.Decoy, SkillEffect.Boomerang);
            Skills(result, "nezha", "화첨창의 불꽃|풍화륜 질주|건곤권 산개|혼천릉 회전|풍화륜 궤도|건곤권 귀환", SkillEffect.Fireball, SkillEffect.DashStrike, SkillEffect.MultiShot, SkillEffect.BladeStorm, SkillEffect.Orbit, SkillEffect.Boomerang);
            Skills(result, "erlang_shen", "삼첨양인도|천안의 광탄|효천의 추격우|삼안의 뇌광|제삼안의 표식|효천견 물어오기", SkillEffect.PierceShot, SkillEffect.ArcaneVolley, SkillEffect.RainArrows, SkillEffect.ChainLightning, SkillEffect.HuntMark, SkillEffect.Hook);
            Skills(result, "guan_yu", "청룡의 검무|언월도 대진격|충의의 수호|적토의 돌파|청룡 군기|언월도 지맥참", SkillEffect.BladeStorm, SkillEffect.Shockwave, SkillEffect.ShieldPulse, SkillEffect.DashStrike, SkillEffect.WarBanner, SkillEffect.Quake);
            Skills(result, "nuwa", "황토의 생명|오색석 결계|보천의 오석진|무너진 하늘의 별|오색 꽃씨|오색석 봉인진", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.Sentry, SkillEffect.Meteor, SkillEffect.Bloom, SkillEffect.Mine);
            Skills(result, "dangun", "홍익의 가호|백두의 검기|신단수의 숨결|건국의 검무|백두의 지맥|홍익의 깃발", SkillEffect.ShieldPulse, SkillEffect.Shockwave, SkillEffect.HealPulse, SkillEffect.BladeStorm, SkillEffect.Quake, SkillEffect.WarBanner);
            Skills(result, "jumong", "해모수의 화살|동명 연사|졸본의 화살비|엄리수의 정적|주몽의 도탄|신궁의 표식", SkillEffect.PierceShot, SkillEffect.MultiShot, SkillEffect.RainArrows, SkillEffect.IceNova, SkillEffect.Ricochet, SkillEffect.HuntMark);
            Skills(result, "hwanung", "뇌공의 천벌|풍백의 격풍|운사의 비구름|천부인의 심판|풍백의 회오리|삼사의 구체", SkillEffect.ChainLightning, SkillEffect.Shockwave, SkillEffect.PoisonCloud, SkillEffect.Meteor, SkillEffect.Vortex, SkillEffect.Orbit);
            Skills(result, "princess_bari", "서천의 약수|망자의 인도|무조의 보호|저승강의 고요|서천 꽃밭의 씨앗|저승길 인도", SkillEffect.HealPulse, SkillEffect.LifeDrain, SkillEffect.ShieldPulse, SkillEffect.IceNova, SkillEffect.Bloom, SkillEffect.Hook);
            Skills(result, "jacheongbi", "오곡의 비|세경의 씨앗|풍요의 들판|생명꽃의 은혜|씨앗 함정|풍년의 낫 귀환", SkillEffect.RainArrows, SkillEffect.MultiShot, SkillEffect.PoisonCloud, SkillEffect.HealPulse, SkillEffect.Mine, SkillEffect.Boomerang);
            Skills(result, "bael", "보이지 않는 독무|왕의 은보|첫 인장의 연사|어둠의 헌납|첫 왕의 저주|보이지 않는 그림자", SkillEffect.PoisonCloud, SkillEffect.DashStrike, SkillEffect.ArcaneVolley, SkillEffect.LifeDrain, SkillEffect.Curse, SkillEffect.Decoy);
            Skills(result, "agares", "공작의 지진|멈춤의 진언|도망자의 봉인|언어의 마법진|공작의 대지 균열|도망자 송환", SkillEffect.Shockwave, SkillEffect.IceNova, SkillEffect.ShieldPulse, SkillEffect.Sentry, SkillEffect.Quake, SkillEffect.Hook);
            Skills(result, "vassago", "잃어버린 길의 화살|과거의 잔향우|미래의 사슬|예지의 별탄|예견된 표식|되튀는 예언", SkillEffect.PierceShot, SkillEffect.RainArrows, SkillEffect.ChainLightning, SkillEffect.ArcaneVolley, SkillEffect.HuntMark, SkillEffect.Ricochet);
            Skills(result, "paimon", "왕의 지식진|비밀의 교향|충성의 결계|황금 왕관의 명령|왕관의 궤도|군단의 깃발", SkillEffect.Sentry, SkillEffect.ArcaneVolley, SkillEffect.ShieldPulse, SkillEffect.Meteor, SkillEffect.Orbit, SkillEffect.WarBanner);
            Skills(result, "asmodeus", "욕망의 화염|격노의 칼춤|왕의 맹진|탐욕의 계약|탐욕의 덫|욕망의 낙인", SkillEffect.Fireball, SkillEffect.BladeStorm, SkillEffect.DashStrike, SkillEffect.LifeDrain, SkillEffect.Mine, SkillEffect.Curse);
            Skills(result, "cu_chulainn", "사냥개의 돌진|게이 볼그 투척|리어스트라드|울라의 포효|사냥개의 사슬|전투 광란의 지진", SkillEffect.DashStrike, SkillEffect.PierceShot, SkillEffect.BladeStorm, SkillEffect.Shockwave, SkillEffect.Hook, SkillEffect.Quake);
            Skills(result, "fionn", "피아나 산개|지혜의 일시|사냥대의 포위우|연어의 지혜|피아나 도탄|사냥개 브란", SkillEffect.MultiShot, SkillEffect.PierceShot, SkillEffect.RainArrows, SkillEffect.HealPulse, SkillEffect.Ricochet, SkillEffect.Decoy);
            Skills(result, "lugh", "브류나크의 빛|광명의 연결|태양의 창|발로르의 종언|브류나크 귀환|슬링 도탄", SkillEffect.PierceShot, SkillEffect.ChainLightning, SkillEffect.Fireball, SkillEffect.Meteor, SkillEffect.Boomerang, SkillEffect.Ricochet);
            Skills(result, "morrigan", "까마귀 안개|전장의 징수|바이브의 날개|운명의 검은 비|까마귀의 저주|죽음 예언의 표식", SkillEffect.PoisonCloud, SkillEffect.LifeDrain, SkillEffect.ArcaneVolley, SkillEffect.RainArrows, SkillEffect.Curse, SkillEffect.HuntMark);
            Skills(result, "dagda", "되살리는 가마솥|풍요의 성벽|거대한 곤봉|우아이트네의 선율진|하프 선율의 깃발|풍요의 씨앗", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.Shockwave, SkillEffect.Sentry, SkillEffect.WarBanner, SkillEffect.Bloom);
            Skills(result, "zodiac_rat", "자시의 잠행|쥐별의 산개|영리한 함정무|그림자 쌍검무|쥐덫|쥐떼 분신", SkillEffect.DashStrike, SkillEffect.MultiShot, SkillEffect.PoisonCloud, SkillEffect.BladeStorm, SkillEffect.Mine, SkillEffect.Decoy);
            Skills(result, "zodiac_ox", "축시의 지축|황소의 방벽|대지의 돌진|혹한의 인내|황소 발구름|코뚜레 사슬", SkillEffect.Shockwave, SkillEffect.ShieldPulse, SkillEffect.DashStrike, SkillEffect.IceNova, SkillEffect.Quake, SkillEffect.Hook);
            Skills(result, "zodiac_tiger", "인시의 호랑검|맹호의 도약|산군의 포효|사냥의 본능|범의 발톱 낚아채기|사냥감 표식", SkillEffect.BladeStorm, SkillEffect.DashStrike, SkillEffect.Shockwave, SkillEffect.LifeDrain, SkillEffect.Hook, SkillEffect.HuntMark);
            Skills(result, "zodiac_rabbit", "묘시의 약방|월궁의 가호|달토끼의 서리|옥토의 별탄|달토끼 꽃씨|달무리 구체", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.IceNova, SkillEffect.ArcaneVolley, SkillEffect.Bloom, SkillEffect.Orbit);
            Skills(result, "zodiac_dragon", "진시의 뇌운|여의주의 염화|용왕의 강우|승천의 심판|용오름|여의주 궤도", SkillEffect.ChainLightning, SkillEffect.Fireball, SkillEffect.RainArrows, SkillEffect.Meteor, SkillEffect.Vortex, SkillEffect.Orbit);

            Traits(result, "michael", "천군의 용맹|날개짓의 박자|성검의 권능", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "gabriel", "전령의 시야|나팔의 박자|계시의 순환", HeroStat.Range, HeroStat.AttackSpeed, HeroStat.Cooldown);
            Traits(result, "raphael", "약수의 지식|수호의 순례|자비의 광휘", HeroStat.Healing, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "uriel", "불꽃의 지성|정화의 확산|심판의 섭리", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "metatron", "천상의 속기|신성 기하학|세피로트 공명", HeroStat.Cooldown, HeroStat.Area, HeroStat.SkillPower);
            Traits(result, "zeus", "번개의 권위|뇌운의 확장|왕의 재림", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "poseidon", "대양의 힘|파도의 박자|삼지창의 권능", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "hades", "명부의 계약|죽음의 범람|저승의 순환", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "athena", "전술의 정수|팔라스의 기민함|지혜의 승리", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Cooldown);
            Traits(result, "artemis", "은활의 시위|사냥의 호흡|달빛의 사정", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Range);
            Traits(result, "odin", "룬의 해석|세계수의 시야|지혜의 대가", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "thor", "거인 사냥꾼|폭풍의 호흡|묠니르 공명", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "loki", "천의 얼굴|교활한 수법|혼돈의 전염", HeroStat.SkillPower, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "freyja", "황금의 눈물|세이드르 숙련|폴크방의 품", HeroStat.Healing, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "tyr", "전신의 담력|외팔의 숙련|굳건한 맹세", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "shiva", "파괴의 리듬|탄다바의 박자|세 번째 눈", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "vishnu", "수호자의 원반|차크라의 궤도|보존의 순환", HeroStat.Damage, HeroStat.Range, HeroStat.Cooldown);
            Traits(result, "brahma", "베다의 구절|창조의 지평|우주의 호흡", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "ganesha", "지혜의 은총|길을 여는 손|상서로운 결계", HeroStat.Healing, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "hanuman", "금강의 완력|바람의 기세|라마의 충의", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "ra", "정오의 광휘|사막의 지평|태양의 운행", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "osiris", "부활의 밀알|나일의 순환|두아트의 권세", HeroStat.Healing, HeroStat.Cooldown, HeroStat.SkillPower);
            Traits(result, "isis", "마법의 이름|왕좌의 자비|날개의 품", HeroStat.SkillPower, HeroStat.Healing, HeroStat.Area);
            Traits(result, "anubis", "진실의 날|방부의 숙련|저울의 균형", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "horus", "매의 정확함|천공의 날개|왕의 원시", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Range);
            Traits(result, "sun_wukong", "금강불괴의 힘|칠십이변 숙련|제천의 기세", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "nezha", "연꽃의 혼|쌍륜의 박자|삼두육비의 힘", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "erlang_shen", "천안의 통찰|명중의 호흡|삼계의 사정", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Range);
            Traits(result, "guan_yu", "무성의 기백|청룡의 호흡|충의의 울림", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Area);
            Traits(result, "nuwa", "생명의 빚음|오색석의 조율|하늘의 수선", HeroStat.Healing, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "dangun", "건국의 의지|태백의 호흡|홍익의 정신", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Healing);
            Traits(result, "jumong", "신궁의 감각|시위의 리듬|동명의 시야", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Range);
            Traits(result, "hwanung", "천부의 권위|삼사의 조화|신시의 순환", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "princess_bari", "생명수의 정성|저승길의 지혜|무조의 자비", HeroStat.Healing, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "jacheongbi", "씨앗의 힘|들판의 박자|생명꽃의 축복", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Healing);
            Traits(result, "bael", "첫 왕의 마력|은폐의 숙련|어둠의 확장", HeroStat.SkillPower, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "agares", "대지의 언어|귀환의 진언|공작의 권역", HeroStat.SkillPower, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "vassago", "잃어버린 지혜|예지의 호흡|미래의 조망", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Range);
            Traits(result, "paimon", "왕의 비전|학문의 정수|지식의 순환", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "asmodeus", "군주의 완력|불꽃의 박자|계약의 권능", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "cu_chulainn", "울라의 투지|전투 광란|게이 볼그 숙련", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "fionn", "피아나의 용맹|추적자의 호흡|연어의 통찰", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Cooldown);
            Traits(result, "lugh", "만능의 재주|빛창의 궤도|광명의 재림", HeroStat.Damage, HeroStat.Range, HeroStat.Cooldown);
            Traits(result, "morrigan", "전쟁의 예감|까마귀의 군세|운명의 실", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);
            Traits(result, "dagda", "가마솥의 풍요|참나무의 지혜|선신의 자비", HeroStat.Healing, HeroStat.Cooldown, HeroStat.SkillPower);
            Traits(result, "zodiac_rat", "쥐의 지혜|자시의 민첩|척후의 계산", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Cooldown);
            Traits(result, "zodiac_ox", "소의 완력|축시의 끈기|대지의 공명", HeroStat.Damage, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "zodiac_tiger", "호랑이의 용맹|인시의 기세|산군의 권능", HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower);
            Traits(result, "zodiac_rabbit", "토끼의 약학|묘시의 손놀림|월궁의 광휘", HeroStat.Healing, HeroStat.Cooldown, HeroStat.Area);
            Traits(result, "zodiac_dragon", "용의 위엄|진시의 구름|승천의 순환", HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown);

            PlayerSkill(result, "fireball", "화염구", RewardTier.Silver, SkillEffect.Fireball, 5, 1.5f, 1.8f, 1, 0);
            PlayerSkill(result, "ice", "빙결 고리", RewardTier.Silver, SkillEffect.IceNova, 6, .9f, 3.2f, 1, 2);
            PlayerSkill(result, "shockwave", "검의 파동", RewardTier.Silver, SkillEffect.Shockwave, 4, 1.25f, 3.5f, 4, 0);
            PlayerSkill(result, "chain", "연쇄 번개", RewardTier.Gold, SkillEffect.ChainLightning, 8, 1.2f, 6, 4, 0);
            PlayerSkill(result, "volley", "비전 연사", RewardTier.Gold, SkillEffect.ArcaneVolley, 7, .75f, 8, 4, 0);
            PlayerSkill(result, "bladestorm", "검의 폭풍", RewardTier.Gold, SkillEffect.BladeStorm, 8, .8f, 3, 4, 1.2f);
            PlayerSkill(result, "meteor", "낙성", RewardTier.Diamond, SkillEffect.Meteor, 12, 3, 4, 1, 0);
            PlayerSkill(result, "dash", "섬광 돌진", RewardTier.Diamond, SkillEffect.DashStrike, 9, 2.3f, 4.5f, 1, 0);
            // One tool for each weapon build the hero may lean into: a chain for melee, a disc for range, orbs and seals that fight alone.
            PlayerSkill(result, "hook", "사슬 갈고리", RewardTier.Silver, SkillEffect.Hook, 7, 1.3f, 5.5f, 1, .6f);
            PlayerSkill(result, "boomerang", "귀환 원반", RewardTier.Silver, SkillEffect.Boomerang, 6, .95f, 6, 1, 0);
            PlayerSkill(result, "orbit", "수호 구체", RewardTier.Gold, SkillEffect.Orbit, 10, .5f, 1.3f, 2, 5);
            PlayerSkill(result, "mine", "봉인 지뢰", RewardTier.Gold, SkillEffect.Mine, 9, 1.3f, 1.3f, 2, 8);

            // Stat cards arrive on every floor, so each one is a modest step that stacks over the climb.
            PlayerTrait(result, "might", "힘의 인장", RewardTier.Silver, HeroStat.Damage, .08f);
            PlayerTrait(result, "vitality", "생명의 그릇", RewardTier.Silver, HeroStat.MaxHealth, .08f);
            PlayerTrait(result, "armor", "강철 피부", RewardTier.Silver, HeroStat.Armor, 4);
            PlayerTrait(result, "swiftness", "가벼운 발걸음", RewardTier.Silver, HeroStat.MoveSpeed, .06f);
            PlayerTrait(result, "frenzy", "전투의 박자", RewardTier.Silver, HeroStat.AttackSpeed, .08f);
            PlayerTrait(result, "reach", "긴 칼날", RewardTier.Gold, HeroStat.Range, .1f);
            PlayerTrait(result, "precision", "치명적 직감", RewardTier.Gold, HeroStat.CritChance, .06f);
            PlayerTrait(result, "vampire", "붉은 갈증", RewardTier.Gold, HeroStat.LifeSteal, .03f);
            PlayerTrait(result, "focus", "명상의 호흡", RewardTier.Gold, HeroStat.Cooldown, .05f);
            PlayerTrait(result, "arcana", "마력의 샘", RewardTier.Gold, HeroStat.SkillPower, .12f);
            PlayerTrait(result, "echo", "메아리 탄환", RewardTier.Diamond, HeroStat.Projectiles, 1);
            PlayerTrait(result, "renewal", "불멸의 숨결", RewardTier.Diamond, HeroStat.Regeneration, 1);
            PlayerTrait(result, "evasion", "찰나의 회피", RewardTier.Diamond, HeroStat.DodgeCooldown, .08f);
            PlayerTrait(result, "expansion", "광역 공명", RewardTier.Diamond, HeroStat.Area, .12f);
            PlayerTrait(result, "grace", "빛의 축복", RewardTier.Diamond, HeroStat.Healing, .15f);
            return result.AsReadOnly();
        }

        private static void Skills(List<AugmentDefinition> result, string owner, string names, params SkillEffect[] effects)
        {
            string[] titles = names.Split('|');
            var companion = FindCompanion(owner);
            int rank = (companion.ArtIndex - 100) % 5;
            float signature = 1 + rank * .035f;
            for (int i = 0; i < effects.Length; i++)
            {
                SkillEffect effect = effects[i];
                // The fifth and sixth skills are the narrower, stranger tools; they keep their own modest numbers.
                if (i >= 4)
                {
                    var odd = OddSkill(effect);
                    result.Add(new AugmentDefinition(owner + ".skill" + (i + 1), owner, titles[i], EffectText(effect), i == 4 ? RewardTier.Silver : RewardTier.Gold,
                        effect, odd.Cooldown + rank * .15f, odd.Power * signature, odd.Radius + rank * .05f, odd.Count, odd.Duration));
                    continue;
                }
                float cooldown = 4.5f + i * 2.5f + rank * .15f;
                float power = effect == SkillEffect.BladeStorm || effect == SkillEffect.ArcaneVolley || effect == SkillEffect.MultiShot || effect == SkillEffect.RainArrows ? .65f :
                    effect == SkillEffect.Meteor ? 2.8f : effect == SkillEffect.ShieldPulse ? 1.8f : effect == SkillEffect.HealPulse ? .8f : 1.3f;
                int count = effect == SkillEffect.MultiShot || effect == SkillEffect.ArcaneVolley ? 3 : effect == SkillEffect.BladeStorm || effect == SkillEffect.ChainLightning || effect == SkillEffect.PierceShot || effect == SkillEffect.Shockwave ? 4 : effect == SkillEffect.RainArrows ? 5 : effect == SkillEffect.Sentry ? 4 : 1;
                float radius = effect == SkillEffect.Fireball ? 1.8f : effect == SkillEffect.PoisonCloud ? 2.8f :
                    effect == SkillEffect.ArcaneVolley || effect == SkillEffect.MultiShot || effect == SkillEffect.PierceShot || effect == SkillEffect.Sentry ? 8 : effect == SkillEffect.ChainLightning || effect == SkillEffect.LifeDrain ? 6 : 3.5f;
                float duration = effect == SkillEffect.IceNova ? 2 : effect == SkillEffect.PoisonCloud ? 4 : effect == SkillEffect.ShieldPulse ? 5 : effect == SkillEffect.Sentry ? 7 : effect == SkillEffect.RainArrows || effect == SkillEffect.BladeStorm ? 1.2f : 0;
                RewardTier tier = i < 2 ? RewardTier.Silver : i == 2 ? RewardTier.Gold : RewardTier.Diamond;
                result.Add(new AugmentDefinition(owner + ".skill" + (i + 1), owner, titles[i], EffectText(effect), tier,
                    effect, cooldown, power * signature, radius + rank * .05f, count, duration));
            }
        }

        private static void Traits(List<AugmentDefinition> result, string owner, string names, params HeroStat[] stats)
        {
            string[] titles = names.Split('|');
            for (int i = 0; i < stats.Length; i++)
            {
                float value = stats[i] == HeroStat.Cooldown ? .05f : i == 2 ? .15f : .09f;
                result.Add(new AugmentDefinition(owner + ".trait" + (i + 1), owner, titles[i], (RewardTier)i, stats[i], value));
            }
        }

        private static CompanionDefinition FindCompanion(string id)
        { foreach (var c in Companions) if (c.Id == id) return c; throw new ArgumentException(id); }
        private static void PlayerSkill(List<AugmentDefinition> list, string id, string name, RewardTier tier, SkillEffect effect, float cd, float power, float radius, int count, float duration)
        { list.Add(new AugmentDefinition("player." + id, "player", name, EffectText(effect), tier, effect, cd, power, radius, count, duration)); }
        private static void PlayerTrait(List<AugmentDefinition> list, string id, string name, RewardTier tier, HeroStat stat, float value)
        { list.Add(new AugmentDefinition("player." + id, "player", name, tier, stat, value)); }
        private static string EffectText(SkillEffect effect)
        {
            string[] text = {
                "충돌 지점에서 폭발하는 화염탄을 발사합니다.", "냉기 폭발로 주변 적에게 피해를 주고 둔화합니다.",
                "여러 적을 잇는 번개가 연쇄합니다.", "표적 지역에 강력한 운석을 떨어뜨립니다.",
                "여러 마력탄을 연속으로 발사합니다.", "전방 충격파로 범위 피해를 줍니다.",
                "주변 적에게 연속 검격을 가합니다.", "적을 향한 돌진 검격으로 피해를 줍니다.",
                "여러 화살을 퍼뜨려 발사합니다.", "적을 관통하는 강력한 투사체를 발사합니다.",
                "표적 지역에 화살비를 내립니다.", "독 구름이 지속적으로 범위 피해를 줍니다.",
                "플레이어의 생명력을 회복합니다.", "플레이어에게 피해를 흡수하는 보호막을 부여합니다.",
                "적의 생명력을 흡수해 플레이어를 치유합니다.", "자동 포탑이 일정 시간 투사체를 발사합니다.",
                "날아갔다 되돌아오는 원반으로 오가며 벱니다.", "적 사이를 튀어 다니는 탄환을 쏩니다.",
                "적에게 표식을 남겨 파티가 주는 피해를 늘립니다.", "가장 먼 적을 사슬로 끌어와 기절시킵니다.",
                "앞으로 번지는 땅의 균열이 적을 띄워 기절시킵니다.", "몸 주위를 도는 구체가 닿는 적을 칩니다.",
                "환영 분신이 곁의 적을 베다가 사라지며 터집니다.", "밟으면 터지는 봉인 지뢰를 앞에 깝니다.",
                "작은 소용돌이가 적을 모았다가 기절시킵니다.", "적에게 저주를 걸어 지속 피해를 줍니다.",
                "깃발 곁의 영웅이 더 빠르게 공격합니다.", "적에게 씨앗을 심어 피어날 때 터지고 치유합니다." };
            return text[(int)effect];
        }
        private struct OddNumbers { public float Cooldown, Power, Radius, Duration; public int Count; }
        // Narrow on purpose: small areas, single targets or short holds, so each one is a tool for a build rather than a nuke.
        private static OddNumbers OddSkill(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.Boomerang: return Odd(7, .85f, 6, 1, 0);
                case SkillEffect.Ricochet: return Odd(7.5f, .8f, 4.5f, 3, 0);
                case SkillEffect.HuntMark: return Odd(9, .5f, 8, 1, 5);
                case SkillEffect.Hook: return Odd(8, 1.05f, 5.5f, 1, .6f);
                case SkillEffect.Quake: return Odd(8, .95f, 4.5f, 1, .35f);
                case SkillEffect.Orbit: return Odd(10, .42f, 1.3f, 2, 5);
                case SkillEffect.Decoy: return Odd(11, .55f, 1.3f, 1, 4);
                case SkillEffect.Mine: return Odd(9, 1.15f, 1.3f, 2, 8);
                case SkillEffect.Vortex: return Odd(10, .3f, 1.8f, 1, 1.6f);
                case SkillEffect.Curse: return Odd(9, .35f, 7, 1, 4);
                case SkillEffect.WarBanner: return Odd(12, .25f, 2.6f, 1, 6);
                case SkillEffect.Bloom: return Odd(9, .9f, 1.6f, 1, 1.2f);
                default: throw new ArgumentException("Not an odd skill: " + effect);
            }
        }
        private static OddNumbers Odd(float cooldown, float power, float radius, int count, float duration)
        { return new OddNumbers { Cooldown = cooldown, Power = power, Radius = radius, Count = count, Duration = duration }; }
    }
}
