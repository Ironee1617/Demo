using System;

namespace PrototypeLab.Roguelike
{
    // The weapon family a skill belongs to. Melee, ranged and automatic weapons will each empower their own style;
    // spells and support skills round out any of them. The party's learned levels per style feed the build resonance.
    public enum SkillStyle { Melee, Ranged, Auto, Spell, Support }
    // Elements decide which statuses a hit leaves and which reactions it can set off.
    public enum SkillElement { None, Fire, Frost, Lightning, Poison }

    /// <summary>
    /// Build rules shared by the cards, the skills and the HUD: each skill's style and element, how much each level adds
    /// (every level is a visible step and level three awakens a new behaviour), the party's style resonance, and the
    /// status reactions that let companions' skills feed each other.
    /// </summary>
    public static class TowerSkillBuilds
    {
        public const int StyleCount = 5;
        // Party levels of one style needed for each resonance tier.
        public static readonly int[] ResonanceThresholds = { 3, 6, 10 };

        public static SkillStyle Style(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.Shockwave: case SkillEffect.BladeStorm: case SkillEffect.DashStrike: case SkillEffect.Hook: case SkillEffect.Quake: return SkillStyle.Melee;
                case SkillEffect.ArcaneVolley: case SkillEffect.MultiShot: case SkillEffect.PierceShot: case SkillEffect.RainArrows:
                case SkillEffect.Boomerang: case SkillEffect.Ricochet: case SkillEffect.HuntMark: return SkillStyle.Ranged;
                case SkillEffect.Sentry: case SkillEffect.Orbit: case SkillEffect.Decoy: case SkillEffect.Mine: return SkillStyle.Auto;
                case SkillEffect.HealPulse: case SkillEffect.ShieldPulse: case SkillEffect.LifeDrain: case SkillEffect.WarBanner: case SkillEffect.Bloom: return SkillStyle.Support;
                default: return SkillStyle.Spell;
            }
        }
        public static SkillElement Element(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.Fireball: case SkillEffect.Meteor: return SkillElement.Fire;
                case SkillEffect.IceNova: return SkillElement.Frost;
                case SkillEffect.ChainLightning: return SkillElement.Lightning;
                case SkillEffect.PoisonCloud: return SkillElement.Poison;
                default: return SkillElement.None;
            }
        }
        public static string StyleName(SkillStyle style)
        { string[] names = { "근접", "원거리", "자동", "주문", "수호" }; return names[(int)style]; }
        public static string ElementName(SkillElement element)
        { string[] names = { "", "화염", "냉기", "번개", "독" }; return names[(int)element]; }
        /// <summary>The tag a card shows: style, then element when there is one.</summary>
        public static string Tag(SkillEffect effect)
        { var element = Element(effect); return StyleName(Style(effect)) + (element == SkillElement.None ? "" : " · " + ElementName(element)); }
        // Projectile skills grow with extra projectiles (cards and ranged resonance); pulses and areas do not.
        public static bool Projectile(SkillEffect effect)
        {
            return effect == SkillEffect.Fireball || effect == SkillEffect.ArcaneVolley || effect == SkillEffect.MultiShot || effect == SkillEffect.PierceShot ||
                effect == SkillEffect.RainArrows || effect == SkillEffect.Sentry || effect == SkillEffect.Shockwave || effect == SkillEffect.Boomerang || effect == SkillEffect.Ricochet;
        }
        public static bool Support(SkillEffect effect) { return effect == SkillEffect.HealPulse || effect == SkillEffect.ShieldPulse; }

        // ------------------------------------------------------------ levels

        // Power, cooldown, area and duration per level (index 0 unused).
        private static readonly float[] LevelPower = { 1, 1, 1.45f, 2 }, LevelCooldown = { 1, 1, .87f, .75f }, LevelArea = { 1, 1, 1.12f, 1.25f }, LevelDuration = { 1, 1, 1.25f, 1.5f };
        private static int Clamp(int level) { return Math.Max(1, Math.Min(3, level)); }
        public static float PowerScale(int level) { return LevelPower[Clamp(level)]; }
        public static float CooldownScale(int level) { return LevelCooldown[Clamp(level)]; }
        public static float DurationScale(int level) { return LevelDuration[Clamp(level)]; }
        public static float AreaScale(SkillEffect effect, int level)
        {
            level = Clamp(level); float scale = LevelArea[level];
            // Single big blows widen instead of multiplying.
            if (level >= 2 && (effect == SkillEffect.Meteor || effect == SkillEffect.DashStrike || effect == SkillEffect.Quake)) scale *= 1.2f;
            return scale;
        }
        /// <summary>Projectiles, chains, targets or hits a level adds on top of the card.</summary>
        public static int ExtraCount(SkillEffect effect, int level)
        {
            level = Clamp(level); if (level == 1) return 0;
            switch (effect)
            {
                case SkillEffect.ChainLightning: case SkillEffect.MultiShot: case SkillEffect.Orbit: case SkillEffect.Mine: return level == 2 ? 1 : 2;
                case SkillEffect.ArcaneVolley: case SkillEffect.Ricochet: return level == 2 ? 1 : 3;
                case SkillEffect.RainArrows: return level == 2 ? 2 : 4;
                case SkillEffect.Decoy: return level == 3 ? 1 : 0;
                case SkillEffect.Fireball: case SkillEffect.Shockwave: case SkillEffect.BladeStorm: case SkillEffect.PierceShot: case SkillEffect.LifeDrain:
                case SkillEffect.Sentry: case SkillEffect.Boomerang: case SkillEffect.HuntMark: case SkillEffect.Hook: case SkillEffect.Curse: case SkillEffect.Bloom: return 1;
                default: return 0;
            }
        }
        /// <summary>The awakened behaviour a skill gains at level three.</summary>
        public static bool Awakened(int level) { return level >= 3; }

        /// <summary>What a level brings, one short line for the card; level one shows the awakening ahead.</summary>
        public static string LevelText(SkillEffect effect, int level)
        {
            string[] second = {
                "화염탄 +1", "범위 +12% · 둔화 +25%", "연쇄 +1", "폭발 범위 +34%", "마력탄 +1", "관통 +1", "타격 +1", "돌진 범위 +34%",
                "화살 +1", "관통 +1", "화살 +2", "독 간격 단축", "회복의 25%가 보호막으로", "보호막량 +15%", "흡수 대상 +1", "포탑 연사 +1",
                "원반 +1", "도탄 +1", "표식 대상 +1", "끌어오는 적 +1", "균열 길이 +34%", "구체 +1", "분신 공격 속도 +40%", "지뢰 +1",
                "끌어당김 강화", "저주 대상 +1", "공격 속도 버프 +15%p", "씨앗 +1" };
            string[] third = {
                "착탄 지점이 3초간 불탑니다", "적을 1초간 얼립니다", "연쇄 +1 · 마지막 적에게 낙뢰 폭발", "작은 운석 2개가 함께 떨어집니다",
                "마력탄이 2발 더 늘어납니다", "0.25초 뒤 두 번째 파동", "주변 적을 끌어당깁니다", "착지 지점에 십자 베기",
                "화살 +1 · 모든 화살 관통", "무한 관통 · 판정 확대", "화살 +2 · 맞은 적 둔화", "구름이 가까운 적을 따라갑니다",
                "3초간 생명의 장판", "보호막이 깨질 때 충격파", "흡수 회복 2배", "포탑 2기 설치",
                "돌아올 때 피해 1.5배", "도탄 +2 · 튈 때마다 피해 증가", "표식 적 처치 시 표식 전이", "끌려온 자리에 충격파",
                "끝에서 바위 기둥 폭발", "구체 +1 · 지속 증가", "분신 2체", "지뢰 +1 · 폭발한 자리 둔화",
                "끝날 때 폭발", "처치 시 저주가 두 적에게 번집니다", "깃발 안 영웅 피해 +15%", "꽃이 필 때 회복 2배" };
            int index = (int)effect;
            return level == 2 ? "Lv.2 · " + second[index] : "Lv.3 각성 · " + third[index];
        }

        // ------------------------------------------------------------ resonance

        /// <summary>Resonance tier (0 to 3) for a style the party holds <paramref name="levels"/> skill levels of.</summary>
        public static int Tier(int levels)
        { int tier = 0; foreach (int threshold in ResonanceThresholds) if (levels >= threshold) tier++; return tier; }
        public static int NextThreshold(int levels)
        { foreach (int threshold in ResonanceThresholds) if (levels < threshold) return threshold; return 0; }
        public static string ResonanceName(SkillStyle style)
        { string[] names = { "칼날의 공명", "사냥의 공명", "기계장치의 공명", "비전의 공명", "수호의 공명" }; return names[(int)style]; }

        private static readonly float[,] PowerBonus = { { 0, .1f, .2f, .35f }, { 0, .1f, .2f, .3f }, { 0, 0, .1f, .2f }, { 0, .1f, .2f, .3f }, { 0, .15f, .3f, .5f } };
        public static float ResonancePower(SkillStyle style, int tier) { return 1 + PowerBonus[(int)style, Math.Max(0, Math.Min(3, tier))]; }
        public static float ResonanceArea(SkillStyle style, int tier) { return style == SkillStyle.Melee ? 1 + (tier >= 3 ? .2f : tier >= 2 ? .1f : 0) : 1; }
        public static float ResonanceDuration(SkillStyle style, int tier) { return style == SkillStyle.Auto ? 1 + (tier >= 3 ? .5f : tier >= 2 ? .3f : tier >= 1 ? .15f : 0) : 1; }
        public static int ResonanceCount(SkillStyle style, int tier) { return style == SkillStyle.Ranged ? Math.Max(0, tier - 1) : 0; }
        public static float ResonanceCooldown(SkillStyle style, int tier)
        { return style == SkillStyle.Spell || style == SkillStyle.Support ? 1 - (tier >= 3 ? .15f : tier >= 2 ? .08f : 0) : 1; }
        public static string ResonanceText(SkillStyle style, int tier)
        {
            tier = Math.Max(1, Math.Min(3, tier));
            string power = "위력 +" + (int)Math.Round(PowerBonus[(int)style, tier] * 100) + "%";
            switch (style)
            {
                case SkillStyle.Melee: return "근접 스킬 " + power + (tier >= 2 ? " · 범위 +" + (tier >= 3 ? 20 : 10) + "%" : "");
                case SkillStyle.Ranged: return "원거리 스킬 " + power + (tier >= 2 ? " · 투사체 +" + (tier - 1) : "");
                case SkillStyle.Auto: return "자동 스킬 지속 +" + (tier >= 3 ? 50 : tier >= 2 ? 30 : 15) + "%" + (tier >= 2 ? " · " + power : "");
                case SkillStyle.Spell: return "주문 스킬 " + power + (tier >= 2 ? " · 재사용 -" + (tier >= 3 ? 15 : 8) + "%" : "");
                default: return "회복·보호막·흡수 " + power + (tier >= 2 ? " · 재사용 -" + (tier >= 3 ? 15 : 8) + "%" : "");
            }
        }

        // ------------------------------------------------------------ reactions

        /// <summary>The status combos, as the help and the practice range explain them.</summary>
        public static readonly string[] Reactions = {
            "초전도  ·  번개가 둔화·빙결된 적에게 1.5배",
            "증기 폭발  ·  화염이 둔화된 적에게, 냉기가 불타는 적에게 1.4배",
            "무방비  ·  근접 스킬이 기절한 적에게 1.35배 (보스는 기절하지 않음)",
            "약점 노출  ·  표식이 붙은 적은 파티의 모든 피해 +20%",
            "독연  ·  독이 불타는 적에게 1.5배",
            "전이  ·  저주는 적이 쓰러지면 옆 적에게 번지고(Lv.3 둘), 표식은 Lv.3부터 옮겨 가며, 씨앗은 그 자리에서 핍니다" };
    }
}
