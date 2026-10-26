using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    // One passive per companion, each drawn from that god's legend.
    public enum PassiveKind
    {
        GiantSlayer, SplitShot, Overheal, Ignite, SkillRelay,
        ChainStrike, Tide, SoulToll, Aegis, Ricochet,
        Gungnir, Mjolnir, Decoy, GoldTears, Resolve,
        Destruction, Discus, Creation, Pathfinder, GroundSlam,
        SunFlare, Rebirth, Wings, Judgment, HawkEye,
        Ruyi, Momentum, ThirdEye, Cleave, SkyStone,
        Hongik, Sureshot, ThreeSeals, Lifewater, Harvest,
        Veil, Quake, Foresight, Melody, Wrath,
        GaeBolg, Insight, LongArm, Omen, Cauldron,
        Nimble, Endure, Roar, MoonHerb, PearlWave
    }

    public sealed class CompanionPassive
    {
        public PassiveKind Kind { get; private set; }
        public string CompanionId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Lore { get; private set; }
        /// <summary>The main magnitude: a damage share, a bonus fraction, a heal or a multiplier.</summary>
        public float Value { get; private set; }
        public float Secondary { get; private set; }
        /// <summary>Health fraction that gates the effect.</summary>
        public float Threshold { get; private set; }
        public float Radius { get; private set; }
        public float Duration { get; private set; }
        /// <summary>Seconds between periodic triggers, or the cooldown of a reactive one.</summary>
        public float Interval { get; private set; }
        /// <summary>Basic attacks per empowered attack.</summary>
        public int Every { get; private set; }
        /// <summary>Targets, bounces, stack cap, blocks or floors, depending on the kind.</summary>
        public int Stacks { get; private set; }
        public bool IsPeriodic { get { return Kind == PassiveKind.Gungnir || Kind == PassiveKind.SunFlare || Kind == PassiveKind.SkyStone || Kind == PassiveKind.ThreeSeals ||
            Kind == PassiveKind.Quake || Kind == PassiveKind.Foresight || Kind == PassiveKind.Melody || Kind == PassiveKind.Roar || Kind == PassiveKind.PearlWave; } }
        public bool HasCooldown { get { return Kind == PassiveKind.Aegis || Kind == PassiveKind.Decoy || Kind == PassiveKind.Wings; } }
        internal CompanionPassive(PassiveKind kind, string companionId, string name, string lore, string text, float value, float secondary,
            float threshold, float radius, float duration, float interval, int every, int stacks)
        {
            Kind = kind; CompanionId = companionId; Name = name; Lore = lore; Value = value; Secondary = secondary; Threshold = threshold;
            Radius = radius; Duration = duration; Interval = interval; Every = every; Stacks = stacks;
            Description = text.Replace("{v}", Percent(value)).Replace("{vn}", N(value)).Replace("{s}", Percent(secondary)).Replace("{sn}", N(secondary))
                .Replace("{t}", Percent(threshold)).Replace("{r}", N(radius)).Replace("{d}", N(duration)).Replace("{i}", N(interval))
                .Replace("{n}", every.ToString()).Replace("{k}", stacks.ToString());
        }
        private static string N(float value) { return AugmentDefinition.Number(value); }
        private static string Percent(float value) { return AugmentDefinition.Number(value * 100); }
    }

    public static class TowerPassiveCatalog
    {
        public const float BaseCriticalMultiplier = 1.75f;
        public static readonly IReadOnlyList<CompanionPassive> Passives = Array.AsReadOnly(new[]
        {
            P(PassiveKind.GiantSlayer, "michael", "불꽃 성검", "악룡을 땅으로 떨어뜨린 천군 대장의 성검.",
                "엘리트와 보스에게 주는 파티 전체 피해 +{v}%", value: .25f),
            P(PassiveKind.SplitShot, "gabriel", "복음의 화살", "하늘의 소식은 한 사람에게만 머물지 않는다.",
                "기본 공격 {n}회마다 화살이 갈라져 다른 적 {k}명에게 {v}% 피해", value: .6f, radius: 8, every: 3, stacks: 2),
            P(PassiveKind.Overheal, "raphael", "여행자의 치유", "토비아의 길동무, 길 위의 상처를 보살핀 치유의 천사.",
                "최대 체력을 넘친 치유량이 {d}초간 보호막이 됩니다 (최대 체력의 {s}%까지)", secondary: .3f, duration: 5),
            P(PassiveKind.Ignite, "uriel", "홍염의 지혜", "에덴의 문 앞에서 불꽃 검을 든 신의 빛.",
                "기본 공격이 적을 {d}초간 불태워 초당 공격력 {v}% 피해", value: .35f, duration: 3),
            P(PassiveKind.SkillRelay, "metatron", "성좌의 기록", "모든 행적을 별자리에 새기는 천상의 서기관.",
                "이 동료가 스킬을 쓸 때마다 파티의 다른 스킬 대기시간 {vn}초 감소", value: 1.5f),

            P(PassiveKind.ChainStrike, "zeus", "뇌정의 권위", "올림포스의 왕은 벼락으로 명령한다.",
                "기본 공격 {n}회마다 번개가 주변 적 {k}명에게 튀어 {v}% 피해", value: .6f, radius: 5, every: 3, stacks: 3),
            P(PassiveKind.Tide, "poseidon", "해일의 삼지창", "삼지창이 땅을 치면 바다가 일어선다.",
                "기본 공격이 적을 {d}초 둔화 · 둔화된 적은 파티에게 피해를 {v}% 더 받음", value: .2f, duration: 1.5f),
            P(PassiveKind.SoulToll, "hades", "명계의 통행세", "명계의 왕은 지나가는 영혼마다 통행세를 받는다.",
                "적이 쓰러질 때마다 체력 {vn} 회복 (엘리트·보스는 {sn})", value: 3, secondary: 15),
            P(PassiveKind.Aegis, "athena", "아이기스의 창", "메두사의 머리를 새긴 방패, 아이기스.",
                "받는 피해 {v}% 감소 · 피격 시 주변 {r} 안의 적 {d}초 석화 ({i}초마다)", value: .12f, radius: 3, duration: 1.2f, interval: 6),
            P(PassiveKind.Ricochet, "artemis", "은빛 추적", "달빛 아래 사냥감은 숨을 곳이 없다.",
                "기본 화살이 맞힌 뒤 가까운 적에게 {k}번 튕겨 {v}% 피해", value: .7f, radius: 5, stacks: 2),

            P(PassiveKind.Gungnir, "odin", "궁니르의 궤적", "던지면 반드시 꿰뚫고 돌아오는 창, 궁니르.",
                "{i}초마다 체력이 가장 많은 적에게 빗나가지 않는 창 · 공격력 {v}% 피해", value: 2.5f, radius: 13, interval: 6),
            P(PassiveKind.Mjolnir, "thor", "묠니르의 무게", "거인의 두개골을 부수는 망치의 무게.",
                "기본 공격 {n}회마다 {vn}배 피해를 주고 {d}초 기절", value: 2, duration: 1.2f, every: 4),
            P(PassiveKind.Decoy, "loki", "기만의 단검", "신들조차 속아 넘어간 변신과 거짓의 달인.",
                "회피하면 환영을 남겨 {d}초 뒤 폭발 · 공격력 {v}% 피해 ({i}초마다)", value: 1.5f, radius: 2.4f, duration: .6f, interval: 2.5f),
            P(PassiveKind.GoldTears, "freyja", "황금의 눈물", "남편을 그리며 흘린 눈물은 붉은 황금이 되었다.",
                "적 처치 시 {v}% 확률로 동전 · 엘리트와 보스는 반드시 금화를 떨어뜨림", value: .25f),
            P(PassiveKind.Resolve, "tyr", "외팔의 결의", "펜리르의 입에 오른손을 내어 준 맹세의 신.",
                "체력 {t}% 이하일 때 파티 피해 +{v}%, 받는 피해 {s}% 감소", value: .3f, secondary: .15f, threshold: .5f),

            P(PassiveKind.Destruction, "shiva", "트리슐라의 춤", "파괴의 춤이 끝나는 곳에서 세계가 다시 태어난다.",
                "파티가 쓰러뜨린 적이 폭발해 주변 {r} 안의 적에게 공격력 {v}% 피해 · 연쇄 폭발", value: .9f, radius: 2.2f),
            P(PassiveKind.Discus, "vishnu", "수다르샤나 원반", "세계를 지키는 신의 손가락 끝에서 도는 원반.",
                "기본 공격 원반이 적 {k}명을 더 관통하고, 꿰뚫을 때마다 피해 +{v}%", value: .25f, stacks: 2),
            P(PassiveKind.Creation, "brahma", "베다의 지식", "네 얼굴로 네 방향을 보며 세계를 빚은 창조신.",
                "스킬을 쓸 때 {v}% 확률로 같은 스킬을 {s}% 위력으로 한 번 더", value: .35f, secondary: .6f),
            P(PassiveKind.Pathfinder, "ganesha", "길잡이의 축복", "모든 시작 앞에서 장애물을 치우는 코끼리 신.",
                "층마다 처음 받는 피해 {k}회를 막고 함정 피해 {v}% 감소", value: .5f, stacks: 1),
            P(PassiveKind.GroundSlam, "hanuman", "금강의 곤봉", "산을 통째로 들고 바다를 건넌 바람의 아들.",
                "기본 공격 {n}회마다 땅을 내리쳐 주변 {r} 안의 적에게 {v}% 피해", value: 1.2f, radius: 2.4f, every: 3),

            P(PassiveKind.SunFlare, "ra", "태양 원반", "낮의 하늘을 가로지르는 태양선의 선장.",
                "{i}초마다 태양 섬광 · 주변 {r} 안의 적에게 {v}% 피해, {d}초 화상", value: 1.2f, secondary: .3f, radius: 4.5f, duration: 2, interval: 7),
            P(PassiveKind.Rebirth, "osiris", "녹색 왕권", "몸이 찢겨도 다시 일어나 명계의 왕이 된 자.",
                "쓰러질 피해를 받으면 체력 {v}%로 부활 · {k}층을 지나면 다시 충전", value: .3f, stacks: 3),
            P(PassiveKind.Wings, "isis", "날개의 가호", "라의 비밀 이름까지 알아낸 마법의 여왕.",
                "체력이 {t}% 아래로 떨어지면 최대 체력 {v}%의 날개 보호막 ({i}초마다)", value: .25f, threshold: .5f, duration: 6, interval: 20),
            P(PassiveKind.Judgment, "anubis", "심판의 지팡이", "깃털과 심장을 저울에 올리는 명계의 재판관.",
                "체력 {t}% 이하 일반 적은 즉시 심판 · 엘리트와 보스는 {s}% 이하에서 피해 +{v}%", value: .2f, secondary: .3f, threshold: .15f),
            P(PassiveKind.HawkEye, "horus", "매의 시선", "해와 달을 두 눈에 품은 하늘의 매.",
                "이 동료의 치명타 확률 +{v}%, 치명타 피해 {sn}배", value: .25f, secondary: 2.5f),

            P(PassiveKind.Ruyi, "sun_wukong", "여의봉의 길이", "용궁에서 뽑아 온, 늘었다 줄었다 하는 쇠몽둥이.",
                "여의봉이 늘어나 앞쪽 {sn}배 거리의 적까지 모두 타격 (추가 대상 {v}%)", value: .7f, secondary: 2),
            P(PassiveKind.Momentum, "nezha", "풍화륜의 속도", "불바퀴 두 개로 하늘을 달리는 소년 장수.",
                "공격할 때마다 이 동료의 공격 속도 +{v}% · 최대 {k}중첩, {d}초 쉬면 초기화", value: .1f, duration: 2.5f, stacks: 5),
            P(PassiveKind.ThirdEye, "erlang_shen", "천안의 조준", "이마의 세 번째 눈은 어떤 변신도 꿰뚫어 본다.",
                "기본 공격에 맞은 적은 {d}초간 파티에게 피해를 {v}% 더 받음", value: .2f, duration: 4),
            P(PassiveKind.Cleave, "guan_yu", "청룡언월도", "여든두 근의 언월도를 휘두르는 충의의 무신.",
                "기본 공격이 대상 주변 {r} 안의 적까지 베어 {v}% 피해", value: .6f, radius: 1.8f),
            P(PassiveKind.SkyStone, "nuwa", "오색석의 생명", "무너진 하늘을 오색 돌로 기워 막은 창조의 여신.",
                "{i}초마다 최대 체력 {v}%의 오색석 보호막 · {s}%까지 쌓임", value: .12f, secondary: .3f, interval: 10),

            P(PassiveKind.Hongik, "dangun", "홍익의 검", "널리 사람을 이롭게 하라는 뜻으로 나라를 연 시조.",
                "함께하는 동료 1명마다 파티 피해 +{v}%", value: .06f),
            P(PassiveKind.Sureshot, "jumong", "백발백중", "일곱 살에 활을 만들어 백 번 쏘아 백 번 맞혔다.",
                "이 동료가 처음 공격하는 적에게는 반드시 치명타"),
            P(PassiveKind.ThreeSeals, "hwanung", "천부인의 권능", "바람·비·구름의 신을 거느리고 신시에 내려온 하늘의 아들.",
                "{i}초마다 풍백·우사·운사가 차례로 · 바람은 밀쳐 둔화, 비는 체력 {s}% 회복, 구름은 적 {k}명에게 낙뢰 {v}%",
                value: 1.5f, secondary: .08f, radius: 5, duration: 2, interval: 8, stacks: 3),
            P(PassiveKind.Lifewater, "princess_bari", "약수의 은총", "버려진 공주가 저승 끝에서 길어 온 생명의 약수.",
                "잃은 체력이 많을수록 이 동료의 치유량 증가 (최대 {vn}배)", value: 3),
            P(PassiveKind.Harvest, "jacheongbi", "풍요의 활", "하늘에서 오곡의 씨앗을 가져온 농사의 여신.",
                "적 처치 시 {v}% 확률로 하트 · 엘리트와 보스는 반드시 하트를 떨어뜨림", value: .15f),

            P(PassiveKind.Veil, "bael", "은폐의 마력", "사람을 보이지 않게 만드는 72군단의 첫 번째 왕.",
                "회피 후 {vn}초 더 모습을 감춰 피해를 받지 않음", value: .45f),
            P(PassiveKind.Quake, "agares", "대지의 진언", "달아나는 자를 되돌리고 땅을 흔드는 동쪽의 공작.",
                "{i}초마다 지진 · 주변 {r} 안의 지상 적에게 {v}% 피해, {d}초 둔화", value: 1, radius: 3.5f, duration: 2, interval: 7),
            P(PassiveKind.Foresight, "vassago", "예지의 사격", "지나간 일과 다가올 일을 모두 보는 예언자.",
                "{i}초마다 플레이어에게 다가오는 적 탄환을 미리 쏘아 떨어뜨림", radius: 5, interval: 1.8f),
            P(PassiveKind.Melody, "paimon", "왕의 선율", "나팔과 심벌즈를 앞세우고 행진하는 지식의 왕.",
                "{i}초마다 왕의 선율 · {d}초간 파티 공격 속도 +{v}%", value: .3f, duration: 4, interval: 10),
            P(PassiveKind.Wrath, "asmodeus", "격노의 대검", "분노와 욕망의 불꽃을 다스리는 지옥의 군주.",
                "적을 쓰러뜨릴 때마다 {d}초간 파티 피해 +{v}% (최대 {k}중첩)", value: .06f, duration: 6, stacks: 5),

            P(PassiveKind.GaeBolg, "cu_chulainn", "게이 볼그", "한 번 박히면 빠지지 않는 가시투성이 마창.",
                "기본 공격이 가시 상처를 남겨 {d}초간 초당 공격력 {v}% 피해 (최대 {k}중첩)", value: .2f, duration: 4, stacks: 3),
            P(PassiveKind.Insight, "fionn", "지혜의 사냥", "지혜의 연어를 맛본 엄지로 모든 길을 읽는 사냥꾼.",
                "기본 공격 {n}회마다 {vn}배 피해의 치명타가 적을 모두 관통", value: 3, every: 5),
            P(PassiveKind.LongArm, "lugh", "빛의 창", "물매돌 하나로 발로르의 마안을 꿰뚫은 긴 팔의 신.",
                "사거리 +{s}% · 멀리 있는 적일수록 이 동료의 피해 증가 (최대 +{v}%)", value: .6f, secondary: .3f),
            P(PassiveKind.Omen, "morrigan", "까마귀의 징조", "까마귀가 내려앉은 자에게는 죽음이 다가온다.",
                "체력 {t}% 이하인 적에게 파티 피해 +{v}%", value: .35f, threshold: .3f),
            P(PassiveKind.Cauldron, "dagda", "생명의 가마솥", "아무리 퍼내도 비지 않는 풍요의 가마솥.",
                "층을 클리어할 때마다 최대 체력 {v}% 추가 회복", value: .3f),

            P(PassiveKind.Nimble, "zodiac_rat", "기민한 척후", "열두 수호자 가운데 가장 먼저 도착한 날랜 척후.",
                "순간 회피 판정 +{vn}초 · 순간 회피하면 회피 대기시간 초기화", value: .1f),
            P(PassiveKind.Endure, "zodiac_ox", "황소의 인내", "묵묵히 걸어 끝내 도착하는 인내의 거신.",
                "피격될 때마다 {d}초간 방어력 +{vn} (최대 {k}중첩)", value: 6, duration: 4, stacks: 5),
            P(PassiveKind.Roar, "zodiac_tiger", "산군의 포효", "산을 다스리는 군주의 울음에 짐승들이 엎드린다.",
                "{i}초마다 포효해 주변 {r} 안의 적 {d}초 기절", radius: 4, duration: 1.2f, interval: 10),
            P(PassiveKind.MoonHerb, "zodiac_rabbit", "월궁의 약초", "달 궁전에서 절구를 찧어 불로초를 빚는 약사.",
                "이 동료가 치유하면 {d}초간 초당 체력 {vn} 재생", value: 2, duration: 4),
            P(PassiveKind.PearlWave, "zodiac_dragon", "여의주의 파동", "구름을 부리고 비를 내리는 하늘의 황제.",
                "{i}초마다 여의주 파동이 적을 모두 관통 · {v}% 피해, {d}초 둔화", value: 2, duration: 1, interval: 10)
        });

        private static readonly Dictionary<string, CompanionPassive> byCompanion = Index();
        private static Dictionary<string, CompanionPassive> Index()
        {
            var index = new Dictionary<string, CompanionPassive>();
            foreach (var passive in Passives) index.Add(passive.CompanionId, passive);
            return index;
        }
        private static CompanionPassive P(PassiveKind kind, string companion, string name, string lore, string text, float value = 0, float secondary = 0,
            float threshold = 0, float radius = 0, float duration = 0, float interval = 0, int every = 0, int stacks = 0)
        { return new CompanionPassive(kind, companion, name, lore, text, value, secondary, threshold, radius, duration, interval, every, stacks); }

        public static CompanionPassive For(string companionId)
        { CompanionPassive passive; return companionId != null && byCompanion.TryGetValue(companionId, out passive) ? passive : null; }
    }

    /// <summary>What a passive needs to know about an enemy being struck.</summary>
    public struct PassiveTarget
    {
        public float HealthFraction, Distance, PlayerHealthFraction;
        public bool Elite, Boss, Slowed, Marked;
    }

    // Run-long bookkeeping for the party's passives: attack counters, stacks, cooldowns and periodic clocks.
    // A passive acts only while its companion is in the party, so a dismissed god takes its passive along.
    public sealed class TowerPassiveState
    {
        private readonly TowerRun run;
        private readonly Dictionary<string, int> attacks = new Dictionary<string, int>();
        private readonly float[] clocks = new float[PassiveKinds], cooldowns = new float[PassiveKinds];
        private static readonly int PassiveKinds = Enum.GetValues(typeof(PassiveKind)).Length;
        private float momentumIdle, wrathTime, endureTime;
        private int seal;
        public int MomentumStacks { get; private set; }
        public int WrathStacks { get; private set; }
        public int EndureStacks { get; private set; }
        public float MelodyRemaining { get; private set; }
        /// <summary>Floors left before the rebirth recharges; zero while it is ready.</summary>
        public int RebirthFloors { get; private set; }
        /// <summary>Hits the pathfinder still turns aside on this floor.</summary>
        public int BlocksLeft { get; private set; }
        internal TowerRun Run { get { return run; } }
        internal TowerPassiveState(TowerRun run) { this.run = run; }

        public CompanionPassive Active(PassiveKind kind)
        { var holder = Holder(kind); return holder == null ? null : TowerPassiveCatalog.For(holder.Id); }
        public bool Has(PassiveKind kind) { return Holder(kind) != null; }
        public CompanionState Holder(PassiveKind kind)
        {
            for (int i = 0; i < run.Companions.Count; i++)
            { var passive = TowerPassiveCatalog.For(run.Companions[i].Id); if (passive != null && passive.Kind == kind) return run.Companions[i]; }
            return null;
        }

        /// <summary>Counts one basic attack of <paramref name="companion"/>; true when this one is its passive's empowered attack.</summary>
        public bool OnBasicAttack(CompanionState companion)
        {
            var passive = InParty(companion) ? TowerPassiveCatalog.For(companion.Id) : null;
            if (passive == null) return false;
            if (passive.Kind == PassiveKind.Momentum) { MomentumStacks = Math.Min(passive.Stacks, MomentumStacks + 1); momentumIdle = 0; }
            if (passive.Every <= 0) return false;
            int count; attacks.TryGetValue(companion.Id, out count); count++;
            attacks[companion.Id] = count % passive.Every;
            return count % passive.Every == 0;
        }
        public int AttacksTowardEmpowered(string companionId)
        { int count; attacks.TryGetValue(companionId, out count); return count; }

        /// <summary>Additive damage bonus the party's passives give <paramref name="attacker"/> against <paramref name="target"/>.</summary>
        public float DamageBonus(HeroState attacker, PassiveTarget target)
        {
            if (attacker == null) return 0;
            float bonus = 0;
            for (int i = 0; i < run.Companions.Count; i++)
            {
                var companion = run.Companions[i]; var p = TowerPassiveCatalog.For(companion.Id); if (p == null) continue;
                switch (p.Kind)
                {
                    case PassiveKind.GiantSlayer: if (target.Elite || target.Boss) bonus += p.Value; break;
                    case PassiveKind.Tide: if (target.Slowed) bonus += p.Value; break;
                    case PassiveKind.ThirdEye: if (target.Marked) bonus += p.Value; break;
                    case PassiveKind.Resolve: if (target.PlayerHealthFraction <= p.Threshold) bonus += p.Value; break;
                    case PassiveKind.Hongik: bonus += p.Value * run.Companions.Count; break;
                    case PassiveKind.Wrath: bonus += p.Value * WrathStacks; break;
                    case PassiveKind.Omen: if (target.HealthFraction <= p.Threshold) bonus += p.Value; break;
                    case PassiveKind.Judgment: if ((target.Elite || target.Boss) && target.HealthFraction <= p.Secondary) bonus += p.Value; break;
                    case PassiveKind.LongArm: if (ReferenceEquals(attacker, companion)) bonus += LongArmBonus(p, target.Distance); break;
                }
            }
            return bonus;
        }
        // Lugh's reach pays off past three units and peaks at nine.
        public static float LongArmBonus(CompanionPassive passive, float distance)
        { return passive.Value * Math.Max(0, Math.Min(1, (distance - 3) / 6)); }
        /// <summary>Multiplier on damage the player takes, before armor and shields.</summary>
        public float DamageTakenScale(float playerHealthFraction)
        {
            float scale = 1;
            var aegis = Active(PassiveKind.Aegis); if (aegis != null) scale *= 1 - aegis.Value;
            var resolve = Active(PassiveKind.Resolve); if (resolve != null && playerHealthFraction <= resolve.Threshold) scale *= 1 - resolve.Secondary;
            return scale;
        }
        public float TrapDamageScale { get { var p = Active(PassiveKind.Pathfinder); return p == null ? 1 : 1 - p.Value; } }
        public float CriticalMultiplier(HeroState attacker)
        { return Owns(attacker, PassiveKind.HawkEye) ? TowerPassiveCatalog.For(attacker.Id).Secondary : TowerPassiveCatalog.BaseCriticalMultiplier; }
        public bool Owns(HeroState hero, PassiveKind kind)
        {
            var companion = hero as CompanionState; if (companion == null) return false;
            var passive = TowerPassiveCatalog.For(companion.Id); return passive != null && passive.Kind == kind && InParty(companion);
        }
        // Stats and strikes ask many times a frame, so party lookups walk the list by index without allocating.
        private bool InParty(CompanionState companion)
        {
            if (companion == null) return false;
            for (int i = 0; i < run.Companions.Count; i++) if (ReferenceEquals(run.Companions[i], companion)) return true;
            return false;
        }
        /// <summary>A normal enemy struck this low is judged at once; elites and bosses are spared.</summary>
        public bool Judges(PassiveTarget target)
        { var p = Active(PassiveKind.Judgment); return p != null && !target.Elite && !target.Boss && target.HealthFraction > 0 && target.HealthFraction <= p.Threshold; }
        public static float LifewaterScale(CompanionPassive passive, float missingFraction)
        { return 1 + (passive.Value - 1) * Math.Max(0, Math.Min(1, missingFraction)); }
        public float DodgeWindowBonus { get { var p = Active(PassiveKind.Nimble); return p == null ? 0 : p.Value; } }

        /// <summary>Stat bonuses that come and go with the fight; the run adds them to each hero's card bonuses.</summary>
        public float StatBonus(HeroState hero, HeroStat stat)
        {
            if (hero == null || run.Companions.Count == 0) return 0;
            float bonus = 0;
            switch (stat)
            {
                case HeroStat.AttackSpeed:
                    var melody = Active(PassiveKind.Melody);
                    if (melody != null && MelodyRemaining > 0) bonus += melody.Value;
                    if (Owns(hero, PassiveKind.Momentum)) bonus += TowerPassiveCatalog.For(hero.Id).Value * MomentumStacks;
                    break;
                case HeroStat.CritChance: if (Owns(hero, PassiveKind.HawkEye)) bonus += TowerPassiveCatalog.For(hero.Id).Value; break;
                case HeroStat.Range: if (Owns(hero, PassiveKind.LongArm)) bonus += TowerPassiveCatalog.For(hero.Id).Secondary; break;
                case HeroStat.Armor:
                    var endure = Active(PassiveKind.Endure);
                    if (endure != null && !(hero is CompanionState)) bonus += endure.Value * EndureStacks;
                    break;
            }
            return bonus;
        }

        public void Tick(float dt)
        {
            if (dt <= 0) return;
            // Frame steps rarely sum to a cooldown exactly; a sliver left over counts as recovered.
            for (int i = 0; i < cooldowns.Length; i++) { float left = cooldowns[i] - dt; cooldowns[i] = left > 1e-4f ? left : 0; }
            MelodyRemaining = Math.Max(0, MelodyRemaining - dt);
            var momentum = Active(PassiveKind.Momentum);
            if (MomentumStacks > 0) { momentumIdle += dt; if (momentum == null || momentumIdle >= momentum.Duration) MomentumStacks = 0; }
            if (WrathStacks > 0) { wrathTime -= dt; if (wrathTime <= 0) WrathStacks = 0; }
            if (EndureStacks > 0) { endureTime -= dt; if (endureTime <= 0) EndureStacks = 0; }
        }
        /// <summary>
        /// Advances a periodic passive's clock; the clock fills even while nothing is in reach, then fires the moment
        /// <paramref name="armed"/> says a target is there. True when it fires.
        /// </summary>
        public bool Periodic(PassiveKind kind, float dt, bool armed)
        {
            var passive = Active(kind);
            if (passive == null || !passive.IsPeriodic) { clocks[(int)kind] = 0; return false; }
            clocks[(int)kind] = Math.Min(passive.Interval, clocks[(int)kind] + Math.Max(0, dt));
            if (!armed || clocks[(int)kind] < passive.Interval) return false;
            clocks[(int)kind] = 0;
            if (kind == PassiveKind.Melody) MelodyRemaining = passive.Duration;
            return true;
        }
        public float PeriodicRemaining(PassiveKind kind)
        { var p = Active(kind); return p == null ? 0 : Math.Max(0, p.Interval - clocks[(int)kind]); }
        /// <summary>Spends a reactive passive's cooldown; false while it is still recovering.</summary>
        public bool TryCooldown(PassiveKind kind)
        {
            var passive = Active(kind);
            if (passive == null || cooldowns[(int)kind] > 0) return false;
            cooldowns[(int)kind] = passive.Interval; return true;
        }
        public float CooldownRemaining(PassiveKind kind) { return cooldowns[(int)kind]; }
        public void OnKill()
        { var p = Active(PassiveKind.Wrath); if (p == null) return; WrathStacks = Math.Min(p.Stacks, WrathStacks + 1); wrathTime = p.Duration; }
        public void OnPlayerHurt()
        { var p = Active(PassiveKind.Endure); if (p == null) return; EndureStacks = Math.Min(p.Stacks, EndureStacks + 1); endureTime = p.Duration; }
        public void OnFloorStart()
        { var p = Active(PassiveKind.Pathfinder); BlocksLeft = p == null ? 0 : p.Stacks; }
        public bool TryBlock()
        { if (!Has(PassiveKind.Pathfinder) || BlocksLeft <= 0) return false; BlocksLeft--; return true; }
        public void OnFloorCleared() { if (RebirthFloors > 0) RebirthFloors--; }
        public bool TryRebirth()
        {
            var p = Active(PassiveKind.Rebirth);
            if (p == null || RebirthFloors > 0) return false;
            RebirthFloors = p.Stacks; return true;
        }
        /// <summary>Hwanung's three seals take turns: 0 wind, 1 rain, 2 cloud.</summary>
        public int NextSeal() { int next = seal; seal = (seal + 1) % 3; return next; }

        /// <summary>A short live line for the party panel; empty when the passive has no state worth showing.</summary>
        public string Status(CompanionState companion)
        {
            var p = companion == null ? null : TowerPassiveCatalog.For(companion.Id);
            if (p == null || !InParty(companion)) return "";
            if (p.IsPeriodic)
            {
                if (p.Kind == PassiveKind.Melody && MelodyRemaining > 0) return "선율 연주 중 " + AugmentDefinition.Number((float)Math.Ceiling(MelodyRemaining)) + "초";
                float remaining = PeriodicRemaining(p.Kind);
                return remaining <= 0 ? "발동 준비됨" : "다음 발동까지 " + AugmentDefinition.Number((float)Math.Ceiling(remaining)) + "초";
            }
            if (p.HasCooldown)
            {
                float remaining = CooldownRemaining(p.Kind);
                return remaining <= 0 ? "발동 준비됨" : "재사용까지 " + AugmentDefinition.Number((float)Math.Ceiling(remaining)) + "초";
            }
            if (p.Every > 0) return "강화 공격까지 " + (p.Every - AttacksTowardEmpowered(companion.Id)) + "회";
            switch (p.Kind)
            {
                case PassiveKind.Rebirth: return RebirthFloors == 0 ? "부활 준비됨" : "재충전까지 " + RebirthFloors + "층";
                case PassiveKind.Pathfinder: return BlocksLeft > 0 ? "이번 층 막기 " + BlocksLeft + "회 남음" : "이번 층 막기 사용함";
                case PassiveKind.Momentum: return "가속 " + MomentumStacks + " / " + p.Stacks;
                case PassiveKind.Wrath: return "격노 " + WrathStacks + " / " + p.Stacks;
                case PassiveKind.Endure: return "인내 " + EndureStacks + " / " + p.Stacks;
                case PassiveKind.Hongik: return "현재 파티 피해 +" + AugmentDefinition.Number(p.Value * run.Companions.Count * 100) + "%";
                default: return "";
            }
        }
    }
}
