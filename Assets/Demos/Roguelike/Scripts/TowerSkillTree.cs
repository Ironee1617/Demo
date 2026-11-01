using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrototypeLab.Roguelike
{
    public enum SkillBranch { Origin, Arcane, Swift, Bond, Fortune, Guard, Might, Heaven, Underworld }
    public enum SkillTarget { Player, Companions, Party }
    // Skill tree effects that are not hero stats; the game applies each where it happens.
    public enum SkillPerk { None, GoldFind, ClearHeal, HeartHeal, OpeningAugment, BossPoints }

    /// <summary>
    /// One node of the village skill tree: its cell on the tree's grid, the node it grows from and what every
    /// learned rank gives, either a hero stat (in <see cref="HeroState"/> bonus units) or a perk.
    /// </summary>
    public sealed class SkillNode
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public SkillBranch Branch { get; private set; }
        public int Column { get; private set; }
        public int Row { get; private set; }
        /// <summary>The node this one grows from; null for the root.</summary>
        public string ParentId { get; private set; }
        public HeroStat Stat { get; private set; }
        public SkillTarget Target { get; private set; }
        public SkillPerk Perk { get; private set; }
        /// <summary>What one rank adds.</summary>
        public float Value { get; private set; }
        public int MaxRank { get; private set; }
        /// <summary>Skill points each rank costs.</summary>
        public int Cost { get; private set; }
        public bool IsRoot { get { return ParentId == null; } }
        /// <summary>A single-rank capstone worth several points, drawn larger on the tree.</summary>
        public bool IsKeystone { get { return MaxRank == 1 && Cost >= 4; } }
        public int TotalCost { get { return MaxRank * Cost; } }
        internal SkillNode(string id, string name, SkillBranch branch, int column, int row, string parent,
            HeroStat stat, SkillTarget target, SkillPerk perk, float value, int maxRank, int cost)
        {
            Id = id; Name = name; Branch = branch; Column = column; Row = row; ParentId = parent;
            Stat = stat; Target = target; Perk = perk; Value = value; MaxRank = maxRank; Cost = cost;
        }
        /// <summary>The effect of <paramref name="ranks"/> learned ranks, e.g. "동료 공격력 +12%".</summary>
        public string Effect(int ranks) { return EffectOf(Value * ranks); }
        internal string EffectOf(float amount)
        {
            switch (Perk)
            {
                case SkillPerk.GoldFind: return "골드 획득 +" + AugmentDefinition.Number(amount * 100) + "%";
                case SkillPerk.ClearHeal: return "층 클리어 회복 +" + AugmentDefinition.Number(amount);
                case SkillPerk.HeartHeal: return "하트 회복량 +" + AugmentDefinition.Number(amount * 100) + "%";
                case SkillPerk.OpeningAugment: return "원정 시작 능력치 증강 +" + AugmentDefinition.Number(amount) + "회";
                case SkillPerk.BossPoints: return "보스 층 스킬 포인트 +" + AugmentDefinition.Number(amount);
            }
            string owner = Target == SkillTarget.Companions ? "동료 " : Target == SkillTarget.Party ? "파티 " : "";
            return owner + AugmentDefinition.StatName(Stat) + " " + AugmentDefinition.FormatValue(Stat, amount);
        }
    }

    /// <summary>
    /// The village skill tree. Every floor an expedition clears earns skill points (kept even when the expedition
    /// fails, unlike its augments); the village spends them on ranks that every later expedition starts with.
    /// The tree grows from one root down a spine of arcane nodes into two crossroads: speed and companions on the
    /// upper one, defence and offence on the lower, fortune between them. Below them the spine runs on to a third,
    /// dearer crossroads where heaven and the underworld part around fortune's last nodes and Lumen's throne.
    /// </summary>
    public static class TowerSkillTree
    {
        public const int Columns = 23, Rows = 20;
        // One point per cleared floor; boss floors (every fifth) and elite floors pay two.
        public const int FloorPoints = 1, BossFloorPoints = 2, EliteFloorPoints = 2;

        public static readonly IReadOnlyList<SkillNode> Nodes = Array.AsReadOnly(Build());
        private static readonly Dictionary<string, SkillNode> byId = Index();

        public static SkillNode Get(string id) { SkillNode node; return id != null && byId.TryGetValue(id, out node) ? node : null; }
        public static SkillNode Root { get { return Nodes[0]; } }
        public static int TotalCost { get { int total = 0; foreach (var node in Nodes) total += node.TotalCost; return total; } }
        public static string BranchName(SkillBranch branch)
        { string[] names = { "근원", "비전", "신속", "유대", "행운", "수호", "전투", "천공", "명계" }; return names[(int)branch]; }

        /// <summary>Skill points for clearing <paramref name="floor"/> of the given type, with the tree's boss-floor perk.</summary>
        public static int PointsFor(int floor, FloorType type, TowerSkillTreeState tree = null)
        {
            if (type == FloorType.Elite) return EliteFloorPoints;
            if (type != FloorType.Normal || floor % TowerRun.RewardInterval != 0) return FloorPoints;
            return BossFloorPoints + (tree == null ? 0 : (int)tree.Perk(SkillPerk.BossPoints));
        }

        private static Dictionary<string, SkillNode> Index()
        {
            var index = new Dictionary<string, SkillNode>();
            foreach (var node in Nodes) index.Add(node.Id, node);
            return index;
        }

        private static SkillNode Stat(string id, string name, SkillBranch branch, int column, int row, string parent,
            HeroStat stat, float value, int maxRank, int cost, SkillTarget target = SkillTarget.Player)
        { return new SkillNode(id, name, branch, column, row, parent, stat, target, SkillPerk.None, value, maxRank, cost); }
        private static SkillNode Perk(string id, string name, int column, int row, string parent, SkillPerk perk, float value, int maxRank, int cost)
        { return new SkillNode(id, name, SkillBranch.Fortune, column, row, parent, HeroStat.Damage, SkillTarget.Player, perk, value, maxRank, cost); }

        private static SkillNode[] Build()
        {
            const SkillBranch O = SkillBranch.Origin, A = SkillBranch.Arcane, S = SkillBranch.Swift, B = SkillBranch.Bond,
                F = SkillBranch.Fortune, G = SkillBranch.Guard, M = SkillBranch.Might, H = SkillBranch.Heaven, U = SkillBranch.Underworld;
            const SkillTarget Allies = SkillTarget.Companions, Party = SkillTarget.Party;
            return new[]
            {
                // The root at the top centre, then the arcane spine with its two wings and the rune row.
                Stat("origin", "루멘의 불씨", O, 11, 0, null, HeroStat.Damage, .05f, 1, 1),
                Stat("arc_seed", "마력의 씨앗", A, 11, 1, "origin", HeroStat.SkillPower, .04f, 3, 1),
                Stat("arc_calm", "명상", A, 10, 1, "arc_seed", HeroStat.Cooldown, .02f, 3, 1),
                Stat("arc_echo", "잔향", A, 9, 1, "arc_calm", HeroStat.Area, .04f, 3, 1),
                Stat("arc_still", "고요한 정신", A, 8, 1, "arc_echo", HeroStat.Cooldown, .03f, 3, 2),
                Stat("arc_void", "무념무상", A, 8, 2, "arc_still", HeroStat.Cooldown, .08f, 1, 4),
                Stat("arc_flow", "마력의 흐름", A, 12, 1, "arc_seed", HeroStat.SkillPower, .04f, 3, 1),
                Stat("arc_wide", "넓은 공명", A, 13, 1, "arc_flow", HeroStat.Area, .05f, 3, 1),
                Stat("arc_volley", "메아리 탄환", A, 14, 1, "arc_wide", HeroStat.Projectiles, 1, 1, 5),
                Stat("arc_core", "비전의 핵", A, 11, 2, "arc_seed", HeroStat.SkillPower, .05f, 3, 2),
                Stat("arc_rune", "룬의 맥", A, 11, 3, "arc_core", HeroStat.Cooldown, .02f, 3, 1),
                Stat("arc_poise", "평정의 룬", A, 10, 3, "arc_rune", HeroStat.Cooldown, .02f, 3, 1),
                Stat("arc_sigil", "확장의 룬", A, 9, 3, "arc_poise", HeroStat.Area, .04f, 3, 1),
                Stat("arc_amp", "증폭의 룬", A, 12, 3, "arc_rune", HeroStat.SkillPower, .04f, 3, 1),
                Stat("arc_ward", "수호의 룬", A, 13, 3, "arc_amp", HeroStat.Healing, .06f, 3, 1),
                Stat("arc_gate", "비전의 관문", A, 11, 4, "arc_rune", HeroStat.SkillPower, .03f, 2, 1),
                // The upper crossroads: speed runs left, the companions' bond runs right.
                Stat("cross_oath", "서약의 교차로", O, 11, 5, "arc_gate", HeroStat.MaxHealth, .05f, 2, 2),
                Stat("sw_step", "가벼운 걸음", S, 10, 5, "cross_oath", HeroStat.MoveSpeed, .02f, 3, 1),
                Stat("sw_tempo", "검의 박자", S, 9, 5, "sw_step", HeroStat.AttackSpeed, .03f, 3, 1),
                Stat("sw_reach", "뻗는 칼끝", S, 8, 5, "sw_tempo", HeroStat.Range, .04f, 3, 1),
                Stat("sw_shade", "긴 그림자", S, 8, 6, "sw_reach", HeroStat.Range, .04f, 3, 1),
                Stat("sw_wind", "바람 걸음", S, 7, 5, "sw_reach", HeroStat.MoveSpeed, .03f, 3, 1),
                Stat("sw_hunt", "사냥꾼의 눈", S, 7, 4, "sw_wind", HeroStat.CritChance, .02f, 3, 1),
                Stat("sw_weak", "약점 간파", S, 7, 3, "sw_hunt", HeroStat.CritChance, .02f, 3, 2),
                Stat("sw_mortal", "일격필살", S, 6, 2, "sw_weak", HeroStat.CritChance, .06f, 1, 4),
                Stat("sw_blink", "찰나", S, 6, 5, "sw_wind", HeroStat.DodgeCooldown, .03f, 3, 1),
                Stat("sw_stance", "반격의 자세", S, 6, 6, "sw_blink", HeroStat.Damage, .03f, 3, 1),
                Stat("sw_flurry", "연격", S, 5, 5, "sw_blink", HeroStat.AttackSpeed, .03f, 3, 2),
                Stat("sw_far", "먼 칼바람", S, 4, 5, "sw_flurry", HeroStat.Range, .05f, 3, 1),
                Stat("sw_gale", "질풍", S, 3, 5, "sw_far", HeroStat.AttackSpeed, .1f, 1, 4),
                Stat("sw_breeze", "바람의 가호", S, 4, 4, "sw_far", HeroStat.MoveSpeed, .03f, 3, 1),
                Stat("sw_shadow", "그림자 도약", S, 4, 3, "sw_breeze", HeroStat.DodgeCooldown, .03f, 3, 1),
                Stat("sw_gust", "칼바람", S, 5, 3, "sw_shadow", HeroStat.AttackSpeed, .03f, 3, 1),
                Stat("sw_after", "잔상", S, 3, 3, "sw_shadow", HeroStat.DodgeCooldown, .04f, 2, 2),
                Stat("sw_dash", "질주", S, 2, 2, "sw_after", HeroStat.MoveSpeed, .1f, 1, 4),
                Stat("bd_call", "동료의 부름", B, 12, 5, "cross_oath", HeroStat.Damage, .04f, 3, 1, Allies),
                Stat("bd_shield", "함께 서는 방패", B, 13, 5, "bd_call", HeroStat.MaxHealth, .05f, 3, 1, Allies),
                Stat("bd_sturdy", "굳건한 동료", B, 13, 6, "bd_shield", HeroStat.Armor, 2, 3, 1, Allies),
                Stat("bd_pace", "발맞춤", B, 14, 5, "bd_shield", HeroStat.AttackSpeed, .03f, 3, 1, Allies),
                Stat("bd_hymn", "신들의 찬가", B, 15, 5, "bd_pace", HeroStat.SkillPower, .05f, 3, 1, Allies),
                Stat("bd_care", "보살핌", B, 15, 6, "bd_hymn", HeroStat.Healing, .06f, 3, 1, Allies),
                Stat("bd_oracle", "신탁", B, 15, 4, "bd_hymn", HeroStat.SkillPower, .04f, 3, 1, Allies),
                Stat("bd_choir", "합창", B, 15, 3, "bd_oracle", HeroStat.Cooldown, .03f, 3, 1, Allies),
                Stat("bd_return", "영웅의 귀환", B, 16, 3, "bd_choir", HeroStat.Area, .05f, 3, 1, Allies),
                Stat("bd_instinct", "신들의 직감", B, 17, 3, "bd_return", HeroStat.CritChance, .02f, 3, 1, Allies),
                Stat("bd_undying", "불멸의 유대", B, 18, 2, "bd_instinct", HeroStat.MaxHealth, .2f, 1, 4, Allies),
                Stat("bd_ward", "수호의 맹세", B, 16, 5, "bd_hymn", HeroStat.Armor, 2, 3, 1, Allies),
                Stat("bd_mercy", "자비의 손길", B, 17, 5, "bd_ward", HeroStat.Healing, .06f, 3, 1, Allies),
                Stat("bd_spring", "치유의 샘", B, 17, 6, "bd_mercy", HeroStat.Healing, .05f, 3, 1, Party),
                Stat("bd_wrath", "신들의 분노", B, 18, 5, "bd_mercy", HeroStat.Damage, .05f, 3, 2, Allies),
                Stat("bd_pantheon", "만신전", B, 19, 5, "bd_wrath", HeroStat.Damage, .06f, 1, 4, Party),
                // Fortune links the two crossroads and spreads into the bottom centre.
                Perk("ft_coin", "황금의 손", 11, 6, "cross_oath", SkillPerk.GoldFind, .1f, 3, 1),
                Perk("ft_feast", "승전의 만찬", 11, 7, "ft_coin", SkillPerk.ClearHeal, 5, 3, 1),
                // The lower crossroads: defence runs left, offence runs right.
                Stat("cross_fate", "운명의 교차로", O, 11, 8, "ft_feast", HeroStat.Damage, .04f, 2, 2),
                Perk("ft_bounty", "풍요", 11, 9, "cross_fate", SkillPerk.GoldFind, .1f, 3, 1),
                Perk("ft_foresight", "선견지명", 10, 10, "ft_bounty", SkillPerk.OpeningAugment, 1, 2, 4),
                Perk("ft_scholar", "현자의 길", 10, 11, "ft_foresight", SkillPerk.BossPoints, 1, 1, 5),
                Perk("ft_treasure", "보물 사냥꾼", 10, 12, "ft_scholar", SkillPerk.GoldFind, .15f, 2, 2),
                Perk("ft_harvest", "생명의 열매", 12, 10, "ft_bounty", SkillPerk.HeartHeal, .2f, 3, 1),
                Stat("ft_blood", "축복받은 피", F, 12, 11, "ft_harvest", HeroStat.Regeneration, .3f, 3, 1),
                Perk("ft_banquet", "불사의 연회", 12, 12, "ft_blood", SkillPerk.ClearHeal, 8, 2, 2),
                Stat("gd_skin", "단단한 피부", G, 10, 8, "cross_fate", HeroStat.Armor, 2, 3, 1),
                Stat("gd_heart", "굳센 심장", G, 9, 8, "gd_skin", HeroStat.MaxHealth, .04f, 3, 1),
                Stat("gd_breath", "숨 고르기", G, 8, 8, "gd_heart", HeroStat.Regeneration, .3f, 3, 1),
                Stat("gd_thirst", "피의 갈증", G, 8, 9, "gd_breath", HeroStat.LifeSteal, .01f, 3, 1),
                Stat("gd_vein", "흡혈의 맥", G, 8, 10, "gd_thirst", HeroStat.LifeSteal, .01f, 3, 2),
                Stat("gd_wall", "철벽", G, 7, 8, "gd_breath", HeroStat.Armor, 3, 3, 1),
                Stat("gd_vessel", "생명의 그릇", G, 6, 8, "gd_wall", HeroStat.MaxHealth, .05f, 3, 1),
                Stat("gd_bulwark", "방벽", G, 6, 9, "gd_vessel", HeroStat.Armor, 2, 3, 1),
                Stat("gd_keep", "성채", G, 6, 10, "gd_bulwark", HeroStat.MaxHealth, .04f, 3, 1),
                Stat("gd_unyielding", "불굴", G, 5, 11, "gd_keep", HeroStat.Armor, 10, 1, 4),
                Stat("gd_prayer", "회복의 기도", G, 7, 11, "gd_keep", HeroStat.Healing, .08f, 3, 1),
                Stat("gd_grace", "은총", G, 5, 8, "gd_vessel", HeroStat.Healing, .06f, 3, 1),
                Stat("gd_root", "뿌리 깊은 나무", G, 4, 8, "gd_grace", HeroStat.Regeneration, .5f, 3, 2),
                Stat("gd_titan", "거신의 몸", G, 3, 8, "gd_root", HeroStat.MaxHealth, .15f, 1, 4),
                Stat("gd_surge", "샘솟는 생명", G, 4, 9, "gd_root", HeroStat.Regeneration, .4f, 3, 1),
                Stat("gd_pulse", "대지의 맥박", G, 4, 10, "gd_surge", HeroStat.MaxHealth, .05f, 3, 2),
                Stat("gd_eternal", "영원의 숨결", G, 3, 11, "gd_pulse", HeroStat.Regeneration, 2, 1, 4),
                Stat("mt_blade", "날 선 검", M, 12, 8, "cross_fate", HeroStat.Damage, .03f, 3, 1),
                Stat("mt_fury", "분노", M, 13, 8, "mt_blade", HeroStat.AttackSpeed, .03f, 3, 1),
                Stat("mt_eye", "급소 찌르기", M, 14, 8, "mt_fury", HeroStat.CritChance, .02f, 3, 1),
                Stat("mt_cleave", "베어 가르기", M, 14, 9, "mt_eye", HeroStat.Range, .04f, 3, 1),
                Stat("mt_rend", "찢는 일격", M, 14, 10, "mt_cleave", HeroStat.Damage, .03f, 3, 1),
                Stat("mt_execute", "처형인", M, 14, 11, "mt_rend", HeroStat.CritChance, .03f, 3, 2),
                Stat("mt_thirst", "붉은 갈증", M, 15, 8, "mt_eye", HeroStat.LifeSteal, .01f, 3, 1),
                Stat("mt_power", "힘의 정수", M, 16, 8, "mt_thirst", HeroStat.Damage, .04f, 3, 1),
                Stat("mt_slaughter", "학살자", M, 17, 8, "mt_power", HeroStat.CritChance, .03f, 3, 2),
                Stat("mt_berserk", "광전사", M, 17, 9, "mt_slaughter", HeroStat.AttackSpeed, .04f, 3, 2),
                Stat("mt_revel", "피의 축제", M, 17, 10, "mt_berserk", HeroStat.LifeSteal, .015f, 2, 2),
                Stat("mt_warcry", "전장의 함성", M, 16, 11, "mt_revel", HeroStat.Damage, .04f, 3, 1),
                Stat("mt_butcher", "도살자", M, 18, 11, "mt_revel", HeroStat.CritChance, .03f, 3, 2),
                Stat("mt_bloodlord", "피의 군주", M, 19, 12, "mt_butcher", HeroStat.LifeSteal, .05f, 1, 4),
                Stat("mt_reaper", "사신의 낫", M, 18, 8, "mt_slaughter", HeroStat.Damage, .05f, 3, 2),
                Stat("mt_godslayer", "신살자", M, 19, 8, "mt_reaper", HeroStat.Damage, .12f, 1, 5),
                // The spine runs on from fortune's bounty to the third crossroads; every rank below costs more.
                Stat("or_stair", "별의 계단", O, 11, 10, "ft_bounty", HeroStat.MaxHealth, .03f, 2, 2),
                Stat("or_bell", "맹세의 종", O, 11, 11, "or_stair", HeroStat.Damage, .03f, 2, 2),
                Stat("or_gate", "천명의 문", O, 11, 12, "or_bell", HeroStat.SkillPower, .03f, 2, 2),
                Stat("cross_heaven", "천명의 교차로", O, 11, 13, "or_gate", HeroStat.MaxHealth, .04f, 2, 3, Party),
                // Heaven runs left: skill power and reach along the top, then angels, companions' hymns and the sun and moon.
                Stat("hv_dawn", "여명", H, 10, 13, "cross_heaven", HeroStat.SkillPower, .04f, 3, 2),
                Stat("hv_halo", "후광", H, 9, 13, "hv_dawn", HeroStat.Cooldown, .02f, 3, 2),
                Stat("hv_star", "별의 인도", H, 8, 13, "hv_halo", HeroStat.Area, .05f, 3, 2),
                Stat("hv_orbit", "천체의 궤도", H, 7, 13, "hv_star", HeroStat.Range, .05f, 3, 2),
                Stat("hv_nova", "초신성", H, 6, 13, "hv_orbit", HeroStat.SkillPower, .05f, 3, 2),
                Stat("hv_comet", "혜성의 꼬리", H, 5, 13, "hv_nova", HeroStat.Area, .06f, 3, 2),
                Stat("hv_meteor", "유성우", H, 4, 13, "hv_comet", HeroStat.Projectiles, 1, 1, 6),
                Stat("hv_choir", "천사의 합창", H, 9, 14, "hv_halo", HeroStat.Healing, .06f, 3, 2, Party),
                Stat("hv_wing", "천상의 날개", H, 9, 15, "hv_choir", HeroStat.MoveSpeed, .03f, 3, 2),
                Stat("hv_feather", "깃털 걸음", H, 9, 16, "hv_wing", HeroStat.DodgeCooldown, .04f, 3, 2),
                Stat("hv_seraph", "세라핌", H, 9, 17, "hv_feather", HeroStat.DodgeCooldown, .12f, 1, 5),
                Stat("hv_herald", "천상의 전령", H, 7, 14, "hv_orbit", HeroStat.SkillPower, .05f, 3, 2, Allies),
                Stat("hv_hymn", "승리의 찬송", H, 7, 15, "hv_herald", HeroStat.Cooldown, .03f, 3, 2, Allies),
                Stat("hv_trumpet", "나팔 소리", H, 7, 16, "hv_hymn", HeroStat.Area, .06f, 3, 2, Allies),
                Stat("hv_psalm", "시편", H, 7, 17, "hv_trumpet", HeroStat.Healing, .08f, 3, 2, Allies),
                Stat("hv_ascend", "승천", H, 8, 18, "hv_psalm", HeroStat.SkillPower, .05f, 3, 3, Party),
                Stat("hv_elysium", "엘리시온", H, 9, 19, "hv_ascend", HeroStat.MaxHealth, .12f, 1, 6, Party),
                Stat("hv_moon", "달의 은총", H, 5, 14, "hv_comet", HeroStat.Healing, .08f, 3, 2),
                Stat("hv_tide", "달의 조수", H, 5, 15, "hv_moon", HeroStat.Cooldown, .03f, 3, 2),
                Stat("hv_eclipse", "일식", H, 4, 16, "hv_tide", HeroStat.Area, .15f, 1, 5),
                Stat("hv_sun", "태양의 권능", H, 5, 16, "hv_tide", HeroStat.SkillPower, .06f, 3, 3),
                Stat("hv_corona", "코로나", H, 5, 17, "hv_sun", HeroStat.Area, .06f, 2, 3),
                Stat("hv_solar", "태양신의 눈", H, 5, 18, "hv_corona", HeroStat.SkillPower, .15f, 1, 6),
                // The underworld mirrors it on the right: blows and blood, the legions of the dead and the fires below.
                Stat("uw_dusk", "황혼", U, 12, 13, "cross_heaven", HeroStat.Damage, .04f, 3, 2),
                Stat("uw_styx", "망자의 강", U, 13, 13, "uw_dusk", HeroStat.CritChance, .02f, 3, 2),
                Stat("uw_ferry", "저승 뱃사공", U, 14, 13, "uw_styx", HeroStat.AttackSpeed, .03f, 3, 2),
                Stat("uw_gate", "명계의 문", U, 15, 13, "uw_ferry", HeroStat.Armor, 3, 3, 2),
                Stat("uw_hound", "지옥의 사냥개", U, 16, 13, "uw_gate", HeroStat.Damage, .05f, 3, 2),
                Stat("uw_flame", "명계의 불꽃", U, 17, 13, "uw_hound", HeroStat.Range, .05f, 3, 2),
                Stat("uw_judge", "명왕의 심판", U, 18, 13, "uw_flame", HeroStat.Damage, .1f, 1, 6),
                Stat("uw_soul", "영혼 흡수", U, 13, 14, "uw_styx", HeroStat.LifeSteal, .01f, 3, 2),
                Stat("uw_ash", "잿빛 재생", U, 13, 15, "uw_soul", HeroStat.Regeneration, .4f, 3, 2),
                Stat("uw_grave", "무덤의 인내", U, 13, 16, "uw_ash", HeroStat.MaxHealth, .05f, 3, 2),
                Stat("uw_lich", "불사의 군주", U, 13, 17, "uw_grave", HeroStat.Regeneration, 2.5f, 1, 5),
                Stat("uw_legion", "망자의 군단", U, 15, 14, "uw_gate", HeroStat.Damage, .05f, 3, 2, Allies),
                Stat("uw_oath", "피의 맹약", U, 15, 15, "uw_legion", HeroStat.LifeSteal, .01f, 3, 2, Allies),
                Stat("uw_shade", "그림자 병사", U, 15, 16, "uw_oath", HeroStat.AttackSpeed, .03f, 3, 2, Allies),
                Stat("uw_banshee", "밴시의 울음", U, 15, 17, "uw_shade", HeroStat.CritChance, .03f, 3, 2, Allies),
                Stat("uw_charon", "저승의 동전", U, 14, 18, "uw_banshee", HeroStat.Damage, .04f, 3, 3, Party),
                Stat("uw_hades", "명왕의 군세", U, 13, 19, "uw_charon", HeroStat.Damage, .1f, 1, 6, Party),
                Stat("uw_brim", "유황불", U, 17, 14, "uw_flame", HeroStat.AttackSpeed, .03f, 3, 2),
                Stat("uw_chain", "저승의 사슬", U, 17, 15, "uw_brim", HeroStat.Armor, 3, 3, 2),
                Stat("uw_cerberus", "케르베로스", U, 18, 16, "uw_chain", HeroStat.Armor, 12, 1, 5),
                Stat("uw_pyre", "화장의 불길", U, 17, 16, "uw_chain", HeroStat.Damage, .05f, 3, 3),
                Stat("uw_inferno", "지옥불", U, 17, 17, "uw_pyre", HeroStat.CritChance, .03f, 2, 3),
                Stat("uw_tartarus", "타르타로스", U, 17, 18, "uw_inferno", HeroStat.Damage, .15f, 1, 6),
                // Fortune's last nodes fall down the middle to the grail and the throne at the very bottom.
                Perk("ft_well", "소원의 샘", 11, 14, "cross_heaven", SkillPerk.GoldFind, .1f, 3, 2),
                Perk("ft_omen", "길조", 11, 15, "ft_well", SkillPerk.HeartHeal, .2f, 3, 2),
                Perk("ft_prophet", "천리안", 10, 15, "ft_omen", SkillPerk.OpeningAugment, 1, 1, 6),
                Perk("ft_offering", "풍년의 공물", 11, 16, "ft_omen", SkillPerk.ClearHeal, 6, 3, 2),
                Perk("ft_sage", "대현자", 12, 16, "ft_offering", SkillPerk.BossPoints, 1, 1, 6),
                Perk("ft_altar", "황금 제단", 11, 17, "ft_offering", SkillPerk.GoldFind, .15f, 2, 3),
                Stat("or_grail", "성배", O, 11, 18, "ft_altar", HeroStat.Healing, .08f, 2, 3, Party),
                Stat("or_throne", "루멘의 신좌", O, 11, 19, "or_grail", HeroStat.Damage, .1f, 1, 8, Party)
            };
        }
    }

    /// <summary>
    /// Ranks learned in the skill tree. A node opens once the node it grows from has a rank; the root is always
    /// open. Every expedition's heroes read their stat bonuses from here and the game reads the perks.
    /// </summary>
    public sealed class TowerSkillTreeState
    {
        private static readonly int StatCount = Enum.GetValues(typeof(HeroStat)).Length;
        private readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        // Stat bonuses are read every frame, so they are summed once per change: [target][stat].
        private readonly float[][] bonus = { new float[StatCount], new float[StatCount], new float[StatCount] };
        private readonly float[] perks = new float[Enum.GetValues(typeof(SkillPerk)).Length];
        public int Spent { get; private set; }
        public int LearnedRanks { get; private set; }
        public int Rank(string id) { int rank; return id != null && ranks.TryGetValue(id, out rank) ? rank : 0; }
        public bool IsOpen(SkillNode node) { return node != null && (node.IsRoot || Rank(node.ParentId) > 0); }
        public bool IsMaxed(SkillNode node) { return node != null && Rank(node.Id) >= node.MaxRank; }
        public bool CanLearn(SkillNode node, int points) { return IsOpen(node) && !IsMaxed(node) && points >= node.Cost; }
        /// <summary>Adds one rank; the caller pays its cost.</summary>
        internal bool Learn(SkillNode node)
        {
            if (!IsOpen(node) || IsMaxed(node)) return false;
            ranks[node.Id] = Rank(node.Id) + 1; Refresh(); return true;
        }
        internal void Clear() { ranks.Clear(); Refresh(); }
        /// <summary>The tree's bonus to <paramref name="stat"/> for the hero: player nodes, companion nodes, and party nodes for both.</summary>
        public float Bonus(HeroState hero, HeroStat stat)
        {
            if (hero == null) return 0;
            int owner = hero is CompanionState ? (int)SkillTarget.Companions : (int)SkillTarget.Player;
            return bonus[owner][(int)stat] + bonus[(int)SkillTarget.Party][(int)stat];
        }
        public float Perk(SkillPerk perk) { return perks[(int)perk]; }

        /// <summary>Every learned effect summed by kind, in tree order, e.g. "공격력 +9%".</summary>
        public IReadOnlyList<string> Summary()
        {
            // The first node of each kind names it; later nodes of the same kind add to its amount.
            var kinds = new List<SkillNode>(); var amounts = new List<float>();
            foreach (var node in TowerSkillTree.Nodes)
            {
                int rank = Rank(node.Id); if (rank == 0) continue;
                int at = kinds.FindIndex(k => k.Perk == node.Perk && (node.Perk != SkillPerk.None || k.Target == node.Target && k.Stat == node.Stat));
                if (at < 0) { kinds.Add(node); amounts.Add(0); at = kinds.Count - 1; }
                amounts[at] += node.Value * rank;
            }
            var lines = new List<string>();
            for (int i = 0; i < kinds.Count; i++) lines.Add(kinds[i].EffectOf(amounts[i]));
            return lines.AsReadOnly();
        }

        private void Refresh()
        {
            foreach (var row in bonus) Array.Clear(row, 0, row.Length);
            Array.Clear(perks, 0, perks.Length); Spent = 0; LearnedRanks = 0;
            foreach (var entry in ranks)
            {
                var node = TowerSkillTree.Get(entry.Key);
                Spent += node.Cost * entry.Value; LearnedRanks += entry.Value;
                if (node.Perk != SkillPerk.None) perks[(int)node.Perk] += node.Value * entry.Value;
                else bonus[(int)node.Target][(int)node.Stat] += node.Value * entry.Value;
            }
        }

        public string Save()
        {
            var entries = new List<string>();
            foreach (var entry in ranks) entries.Add(entry.Key + "=" + entry.Value.ToString(CultureInfo.InvariantCulture));
            entries.Sort(StringComparer.Ordinal);
            return String.Join(",", entries.ToArray());
        }
        /// <summary>Reads <see cref="Save"/>; unknown nodes are dropped, ranks clamp to each node's maximum and nodes cut off from the root fall away.</summary>
        public static TowerSkillTreeState Load(string saved)
        {
            var tree = new TowerSkillTreeState();
            if (!String.IsNullOrEmpty(saved))
                foreach (string entry in saved.Split(','))
                {
                    string[] pair = entry.Split('='); int rank; var node = pair.Length == 2 ? TowerSkillTree.Get(pair[0]) : null;
                    if (node == null || !Int32.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out rank) || rank < 1) continue;
                    tree.ranks[node.Id] = Math.Min(rank, node.MaxRank);
                }
            // Nodes are listed parents first, so one pass in tree order drops every orphan and its descendants.
            foreach (var node in TowerSkillTree.Nodes) if (tree.Rank(node.Id) > 0 && !tree.IsOpen(node)) tree.ranks.Remove(node.Id);
            tree.Refresh();
            return tree;
        }
    }
}
