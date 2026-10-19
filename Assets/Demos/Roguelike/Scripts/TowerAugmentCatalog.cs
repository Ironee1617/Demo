using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public static class TowerAugmentCatalog
    {
        public static readonly IReadOnlyList<CompanionDefinition> Companions = Array.AsReadOnly(new[]
        {
            C("rook", "루크", "견습 기사", "기본 검격으로 가까운 적을 공격합니다.", RewardTier.Silver, CompanionRole.Vanguard, 1, 13, 1.05f, 1.9f, 0, "C6D2DF"),
            C("finn", "핀", "숲의 궁수", "기본 화살로 먼 적을 공격합니다.", RewardTier.Silver, CompanionRole.Archer, 11, 10, 1.2f, 7.5f, 0, "8FD6A3"),
            C("mira", "미라", "빛의 수습생", "빛의 탄환으로 공격하고 5초마다 체력을 치유합니다.", RewardTier.Silver, CompanionRole.Healer, 8, 8, 1.5f, 6.5f, 5, "AEE9D7"),
            C("bram", "브람", "철벽 용병", "무거운 기본 검격으로 앞길을 엽니다.", RewardTier.Gold, CompanionRole.Vanguard, 13, 20, 1.1f, 2.2f, 0, "E5B767"),
            C("sera", "세라", "불꽃 마도사", "기본 마력탄으로 원거리 공격을 합니다.", RewardTier.Gold, CompanionRole.Mage, 4, 17, 1.55f, 7, 0, "F6A475"),
            C("lyra", "리라", "바람 추적자", "빠른 기본 화살로 적을 추적합니다.", RewardTier.Gold, CompanionRole.Archer, 14, 15, .95f, 8, 0, "E4D18D"),
            C("atlas", "아틀라스", "서약의 기사", "묵직한 기본 검격으로 적을 공격합니다.", RewardTier.Diamond, CompanionRole.Vanguard, 6, 27, 1.1f, 2.7f, 0, "A9D9F8"),
            C("noa", "노아", "별의 현자", "기본 별빛 탄환으로 적을 공격합니다.", RewardTier.Diamond, CompanionRole.Mage, 7, 23, 1.4f, 8, 0, "BCAEF4"),
            C("elin", "엘린", "새벽의 성녀", "성광탄으로 공격하고 5초마다 강하게 치유합니다.", RewardTier.Diamond, CompanionRole.Healer, 2, 15, 1.3f, 7.5f, 9, "B5EDF5"),
            C("jax", "잭스", "그림자 척후병", "날렵한 기본 검격으로 적의 빈틈을 노립니다.", RewardTier.Silver, CompanionRole.Vanguard, 3, 11, .85f, 1.8f, 0, "B5A1DB"),
            C("bea", "베아", "거리의 투사", "튼튼한 기본 근접 공격을 사용합니다.", RewardTier.Silver, CompanionRole.Vanguard, 5, 16, 1.25f, 2, 0, "DE9E8E"),
            C("rhea", "레아", "방랑 악사", "공명하는 기본 마력탄을 발사합니다.", RewardTier.Gold, CompanionRole.Mage, 12, 14, 1.15f, 7.2f, 0, "DCB0E4"),
            C("cors", "코르", "하늘 해적", "강력한 기본 사격으로 먼 적을 겨눕니다.", RewardTier.Gold, CompanionRole.Archer, 9, 19, 1.35f, 8.4f, 0, "E8BB83"),
            C("gizmo", "기즈모", "마력 공학자", "정밀한 기본 에너지탄으로 공격합니다.", RewardTier.Diamond, CompanionRole.Mage, 16, 22, 1.2f, 7.8f, 0, "8DDCE1"),
            C("shade", "셰이드", "월영의 검", "빠른 기본 칼날로 가까운 적을 베어냅니다.", RewardTier.Diamond, CompanionRole.Vanguard, 10, 21, .8f, 2.1f, 0, "A7B8EE")
        });

        public static readonly IReadOnlyList<AugmentDefinition> Augments = Build();

        private static CompanionDefinition C(string id, string name, string title, string description, RewardTier tier,
            CompanionRole role, int art, float damage, float interval, float range, float healing, string tint)
        { return new CompanionDefinition(id, name, title, description, tier, role, art, damage, interval, range, healing, tint); }

        private static IReadOnlyList<AugmentDefinition> Build()
        {
            var result = new List<AugmentDefinition>();
            Skills(result, "rook", "초승달 검기|정면 돌파|견습의 맹세|기사의 회전검", SkillEffect.Shockwave, SkillEffect.DashStrike, SkillEffect.ShieldPulse, SkillEffect.BladeStorm);
            Skills(result, "finn", "숲의 산탄|나뭇잎 관통|매의 화살비|독버섯 지대", SkillEffect.MultiShot, SkillEffect.PierceShot, SkillEffect.RainArrows, SkillEffect.PoisonCloud);
            Skills(result, "mira", "작은 기적|빛의 장막|빛무리 탄환|생명의 연결", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.ArcaneVolley, SkillEffect.LifeDrain);
            Skills(result, "bram", "대지 분쇄|철벽의 약속|용병의 난무|강철 돌진", SkillEffect.Shockwave, SkillEffect.ShieldPulse, SkillEffect.BladeStorm, SkillEffect.DashStrike);
            Skills(result, "sera", "폭발 화염구|그을린 구름|불씨 연사|화산의 심판", SkillEffect.Fireball, SkillEffect.PoisonCloud, SkillEffect.ArcaneVolley, SkillEffect.Meteor);
            Skills(result, "lyra", "바람의 산탄|폭풍의 사슬|천공 화살비|서리바람", SkillEffect.MultiShot, SkillEffect.ChainLightning, SkillEffect.RainArrows, SkillEffect.IceNova);
            Skills(result, "atlas", "서약의 검무|왕의 충격파|수호자의 성역|천상의 낙인", SkillEffect.BladeStorm, SkillEffect.Shockwave, SkillEffect.ShieldPulse, SkillEffect.Meteor);
            Skills(result, "noa", "별자리 연사|얼어붙은 은하|성운의 사슬|유성우", SkillEffect.ArcaneVolley, SkillEffect.IceNova, SkillEffect.ChainLightning, SkillEffect.Meteor);
            Skills(result, "elin", "새벽의 기도|성녀의 보호|생명의 성광|여명의 심판", SkillEffect.HealPulse, SkillEffect.ShieldPulse, SkillEffect.LifeDrain, SkillEffect.Meteor);
            Skills(result, "jax", "그림자 돌진|붉은 계약|암영의 난무|밤안개", SkillEffect.DashStrike, SkillEffect.LifeDrain, SkillEffect.BladeStorm, SkillEffect.PoisonCloud);
            Skills(result, "bea", "투사의 진동|거리의 회오리|불굴의 숨결|맹렬한 돌격", SkillEffect.Shockwave, SkillEffect.BladeStorm, SkillEffect.HealPulse, SkillEffect.DashStrike);
            Skills(result, "rhea", "회복의 선율|공명의 연주|진동의 파동|자율의 악장", SkillEffect.HealPulse, SkillEffect.ArcaneVolley, SkillEffect.Shockwave, SkillEffect.Sentry);
            Skills(result, "cors", "선장의 관통탄|화약 연막|산개 사격|함대 일제 사격", SkillEffect.PierceShot, SkillEffect.PoisonCloud, SkillEffect.MultiShot, SkillEffect.RainArrows);
            Skills(result, "gizmo", "마력 연발기|자동 포탑|과부하 파동|궤도 폭격", SkillEffect.ArcaneVolley, SkillEffect.Sentry, SkillEffect.Shockwave, SkillEffect.Meteor);
            Skills(result, "shade", "월영 돌진|초승달 관통|침묵의 독무|만월의 검무", SkillEffect.DashStrike, SkillEffect.PierceShot, SkillEffect.PoisonCloud, SkillEffect.BladeStorm);

            foreach (var c in Companions)
            {
                HeroStat[] stats = c.Role == CompanionRole.Mage ? new[] { HeroStat.SkillPower, HeroStat.Area, HeroStat.Cooldown } :
                    c.Role == CompanionRole.Healer ? new[] { HeroStat.Healing, HeroStat.Cooldown, HeroStat.SkillPower } :
                    c.Role == CompanionRole.Archer ? new[] { HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.Cooldown } :
                    new[] { HeroStat.Damage, HeroStat.AttackSpeed, HeroStat.SkillPower };
                string[] names = { "숙련의 서약", "전장의 호흡", "한계 돌파" };
                for (int i = 0; i < 3; i++)
                {
                    float value = stats[i] == HeroStat.Cooldown ? .1f : i == 2 ? .3f : .18f;
                    result.Add(new AugmentDefinition(c.Id + ".trait" + (i + 1), c.Id, c.Name + "의 " + names[i], (RewardTier)i, stats[i], value));
                }
            }

            PlayerSkill(result, "fireball", "화염구", RewardTier.Silver, SkillEffect.Fireball, 5, 1.5f, 1.8f, 1, 0);
            PlayerSkill(result, "ice", "빙결 고리", RewardTier.Silver, SkillEffect.IceNova, 6, .9f, 3.2f, 1, 2);
            PlayerSkill(result, "shockwave", "검의 파동", RewardTier.Silver, SkillEffect.Shockwave, 4, 1.25f, 3.5f, 4, 0);
            PlayerSkill(result, "chain", "연쇄 번개", RewardTier.Gold, SkillEffect.ChainLightning, 8, 1.2f, 6, 4, 0);
            PlayerSkill(result, "volley", "비전 연사", RewardTier.Gold, SkillEffect.ArcaneVolley, 7, .75f, 8, 4, 0);
            PlayerSkill(result, "bladestorm", "검의 폭풍", RewardTier.Gold, SkillEffect.BladeStorm, 8, .8f, 3, 4, 1.2f);
            PlayerSkill(result, "meteor", "낙성", RewardTier.Diamond, SkillEffect.Meteor, 12, 3, 4, 1, 0);
            PlayerSkill(result, "dash", "섬광 돌진", RewardTier.Diamond, SkillEffect.DashStrike, 9, 2.3f, 4.5f, 1, 0);

            PlayerTrait(result, "might", "힘의 인장", RewardTier.Silver, HeroStat.Damage, .16f);
            PlayerTrait(result, "vitality", "생명의 그릇", RewardTier.Silver, HeroStat.MaxHealth, .15f);
            PlayerTrait(result, "armor", "강철 피부", RewardTier.Silver, HeroStat.Armor, 8);
            PlayerTrait(result, "swiftness", "가벼운 발걸음", RewardTier.Silver, HeroStat.MoveSpeed, .12f);
            PlayerTrait(result, "frenzy", "전투의 박자", RewardTier.Silver, HeroStat.AttackSpeed, .16f);
            PlayerTrait(result, "reach", "긴 칼날", RewardTier.Gold, HeroStat.Range, .2f);
            PlayerTrait(result, "precision", "치명적 직감", RewardTier.Gold, HeroStat.CritChance, .12f);
            PlayerTrait(result, "vampire", "붉은 갈증", RewardTier.Gold, HeroStat.LifeSteal, .06f);
            PlayerTrait(result, "focus", "명상의 호흡", RewardTier.Gold, HeroStat.Cooldown, .1f);
            PlayerTrait(result, "arcana", "마력의 샘", RewardTier.Gold, HeroStat.SkillPower, .25f);
            PlayerTrait(result, "echo", "메아리 탄환", RewardTier.Diamond, HeroStat.Projectiles, 1);
            PlayerTrait(result, "renewal", "불멸의 숨결", RewardTier.Diamond, HeroStat.Regeneration, 2);
            PlayerTrait(result, "evasion", "찰나의 회피", RewardTier.Diamond, HeroStat.DodgeCooldown, .15f);
            PlayerTrait(result, "expansion", "광역 공명", RewardTier.Diamond, HeroStat.Area, .25f);
            PlayerTrait(result, "grace", "빛의 축복", RewardTier.Diamond, HeroStat.Healing, .3f);
            return result.AsReadOnly();
        }

        private static void Skills(List<AugmentDefinition> result, string owner, string names, params SkillEffect[] effects)
        {
            string[] titles = names.Split('|');
            var companion = FindCompanion(owner);
            float signature = 1 + companion.ArtIndex * .012f;
            for (int i = 0; i < effects.Length; i++)
            {
                SkillEffect effect = effects[i];
                float cooldown = 4.5f + i * 2.5f + companion.ArtIndex * .08f;
                float power = effect == SkillEffect.BladeStorm || effect == SkillEffect.ArcaneVolley || effect == SkillEffect.MultiShot || effect == SkillEffect.RainArrows ? .65f :
                    effect == SkillEffect.Meteor ? 2.8f : effect == SkillEffect.ShieldPulse ? 1.8f : effect == SkillEffect.HealPulse ? .8f : 1.3f;
                int count = effect == SkillEffect.MultiShot || effect == SkillEffect.ArcaneVolley ? 3 : effect == SkillEffect.BladeStorm || effect == SkillEffect.ChainLightning || effect == SkillEffect.PierceShot || effect == SkillEffect.Shockwave ? 4 : effect == SkillEffect.RainArrows ? 5 : effect == SkillEffect.Sentry ? 4 : 1;
                float radius = effect == SkillEffect.Fireball ? 1.8f : effect == SkillEffect.PoisonCloud ? 2.8f :
                    effect == SkillEffect.ArcaneVolley || effect == SkillEffect.MultiShot || effect == SkillEffect.PierceShot || effect == SkillEffect.Sentry ? 8 : effect == SkillEffect.ChainLightning || effect == SkillEffect.LifeDrain ? 6 : 3.5f;
                float duration = effect == SkillEffect.IceNova ? 2 : effect == SkillEffect.PoisonCloud ? 4 : effect == SkillEffect.ShieldPulse ? 5 : effect == SkillEffect.Sentry ? 7 : effect == SkillEffect.RainArrows || effect == SkillEffect.BladeStorm ? 1.2f : 0;
                RewardTier tier = i < 2 ? RewardTier.Silver : i == 2 ? RewardTier.Gold : RewardTier.Diamond;
                result.Add(new AugmentDefinition(owner + ".skill" + (i + 1), owner, titles[i], EffectText(effect), tier,
                    effect, cooldown, power * signature, radius + companion.ArtIndex * .025f, count, duration));
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
                "적의 생명력을 흡수해 플레이어를 치유합니다.", "자동 포탑이 일정 시간 투사체를 발사합니다." };
            return text[(int)effect];
        }
    }
}
