using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PrototypeLab.Roguelike
{
    public enum RewardTier { Silver, Gold, Diamond }
    public enum CompanionRole { Vanguard, Archer, Mage, Healer }
    public enum RewardKind { Recruit, Skill, Trait, Upgrade = Trait, Oath = 3 }
    public enum UpgradeStat { Damage, Haste, Ability }
    public enum HeroStat { Damage, MaxHealth, Armor, MoveSpeed, AttackSpeed, Range, CritChance, LifeSteal, Cooldown, SkillPower, Projectiles, Regeneration, DodgeCooldown, Area, Healing }
    public enum SkillEffect { Fireball, IceNova, ChainLightning, Meteor, ArcaneVolley, Shockwave, BladeStorm, DashStrike, MultiShot, PierceShot, RainArrows, PoisonCloud, HealPulse, ShieldPulse, LifeDrain, Sentry }

    public sealed class CompanionDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string MythId { get; private set; }
        public string InnateTrait { get; private set; }
        public string InnateDescription { get; private set; }
        public RewardTier Tier { get; private set; }
        public CompanionRole Role { get; private set; }
        public int ArtIndex { get; private set; }
        public float Damage { get; private set; }
        public float MaxHealth { get; private set; }
        public float Armor { get; private set; }
        public float MoveSpeed { get; private set; }
        public float AttackInterval { get; private set; }
        public float Range { get; private set; }
        public float HealAmount { get; private set; }
        public string TintHex { get; private set; }
        internal CompanionDefinition(string id, string name, string title, string description,
            RewardTier tier, CompanionRole role, int artIndex, float damage, float interval,
            float range, float healing, string tint, string mythId = "eden", string innateTrait = "", string innateDescription = "")
        {
            Id = id; Name = name; Title = title; Description = description;
            Tier = tier; Role = role; ArtIndex = artIndex; Damage = damage;
            AttackInterval = interval; Range = range; HealAmount = healing; TintHex = tint;
            MaxHealth = role == CompanionRole.Vanguard ? 150 : role == CompanionRole.Healer ? 110 : 95;
            Armor = role == CompanionRole.Vanguard ? 10 : 3;
            MoveSpeed = role == CompanionRole.Archer ? 6.5f : 5.5f;
            MythId = mythId; InnateTrait = innateTrait; InnateDescription = innateDescription;
        }
    }

    public sealed class AugmentDefinition
    {
        public string Id { get; private set; }
        public string OwnerId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public RewardKind Kind { get; private set; }
        public RewardTier Tier { get; private set; }
        public SkillEffect Effect { get; private set; }
        public float Cooldown { get; private set; }
        public float Power { get; private set; }
        public float Radius { get; private set; }
        public int Count { get; private set; }
        public float Duration { get; private set; }
        public HeroStat Stat { get; private set; }
        public float Value { get; private set; }
        internal AugmentDefinition(string id, string owner, string name, string description, RewardTier tier,
            SkillEffect effect, float cooldown, float power, float radius, int count, float duration)
        {
            Id = id; OwnerId = owner; Name = name; Description = description; Tier = tier;
            Kind = RewardKind.Skill; Effect = effect; Cooldown = cooldown; Power = power;
            Radius = radius; Count = count; Duration = duration;
        }
        internal AugmentDefinition(string id, string owner, string name, RewardTier tier, HeroStat stat, float value)
        {
            Id = id; OwnerId = owner; Name = name; Tier = tier; Kind = RewardKind.Trait;
            Stat = stat; Value = value;
            Description = StatName(stat) + " " + FormatValue(stat, value) + " · 반복 선택 시 누적";
        }
        public static string StatName(HeroStat stat)
        {
            string[] names = { "공격력", "최대 체력", "방어력", "이동 속도", "공격 속도", "기본 공격 사거리", "치명타 확률", "생명력 흡수", "스킬 재사용 감소", "스킬 위력", "추가 투사체", "초당 체력 재생", "회피 재사용 감소", "스킬 범위", "치유 / 보호막 위력" };
            return names[(int)stat];
        }
        internal static string Number(float value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
        public static string FormatValue(HeroStat stat, float value)
        {
            bool flat = stat == HeroStat.Armor || stat == HeroStat.Projectiles || stat == HeroStat.Regeneration;
            return "+" + Number(flat ? value : value * 100) + (flat ? stat == HeroStat.Regeneration ? " HP/s" : "" : "%");
        }
    }

    public sealed class SkillState
    {
        private readonly HeroState owner;
        public AugmentDefinition Definition { get; private set; }
        public int Level { get; private set; }
        public float Cooldown { get { return CooldownAt(Level); } }
        public float Power { get { return PowerAt(Level); } }
        public float Radius { get { return RadiusAt(Level); } }
        public int Count
        {
            get
            {
                var effect = Definition.Effect;
                bool projectile = effect == SkillEffect.Fireball || effect == SkillEffect.ArcaneVolley || effect == SkillEffect.MultiShot ||
                    effect == SkillEffect.PierceShot || effect == SkillEffect.RainArrows || effect == SkillEffect.Sentry || effect == SkillEffect.Shockwave;
                return Definition.Count + (projectile ? owner.ProjectileBonus : 0);
            }
        }
        public float Duration { get { return Definition.Duration * (1 + .2f * (Level - 1)); } }
        public string Description { get { return Describe(Level); } }
        internal SkillState(AugmentDefinition definition, HeroState owner, int level = 1)
        { Definition = definition; this.owner = owner; Level = level; }
        internal void Upgrade() { if (Level < 3) Level++; }
        public float CooldownAt(int level) { return Math.Max(1, Definition.Cooldown * (1 - .15f * (level - 1)) * owner.CooldownMultiplier); }
        private float PowerAt(int level)
        {
            bool support = Definition.Effect == SkillEffect.HealPulse || Definition.Effect == SkillEffect.ShieldPulse;
            return Definition.Power * (1 + .35f * (level - 1)) * owner.SkillPowerMultiplier * (support ? owner.HealingMultiplier : 1);
        }
        private float RadiusAt(int level) { return Definition.Radius * (1 + .1f * (level - 1)) * owner.AreaMultiplier; }
        internal string Describe(int level)
        {
            string amount = Definition.Effect == SkillEffect.HealPulse ? "회복" : Definition.Effect == SkillEffect.ShieldPulse ? "보호막" : "피해";
            return Definition.Description + "\n" + amount + " " + AugmentDefinition.Number(owner.Damage * PowerAt(level)) +
                " · 범위 " + AugmentDefinition.Number(RadiusAt(level)) + " · 수량 " + Count +
                (Definition.Duration > 0 ? " · 지속 " + AugmentDefinition.Number(Definition.Duration * (1 + .2f * (level - 1))) + "초" : "") +
                "\n재사용 " + AugmentDefinition.Number(CooldownAt(level)) + "초";
        }
    }

    public sealed class TraitState
    {
        private readonly HeroState owner;
        public AugmentDefinition Definition { get; private set; }
        public int Level { get; private set; }
        public float Value { get { return Definition.Value * Level; } }
        public string Description { get { return Definition.Description + "\n누적 " + AugmentDefinition.FormatValue(Definition.Stat, Value) + " · 현재 " + owner.StatText(Definition.Stat); } }
        internal TraitState(AugmentDefinition definition, HeroState owner) { Definition = definition; this.owner = owner; Level = 1; }
        internal void Upgrade() { Level++; }
    }

    public class HeroState
    {
        private readonly float baseDamage, baseHealth, baseArmor, baseMoveSpeed, baseInterval, baseRange;
        private readonly List<SkillState> skills = new List<SkillState>();
        private readonly List<TraitState> traits = new List<TraitState>();
        private readonly ReadOnlyCollection<SkillState> skillView;
        private readonly ReadOnlyCollection<TraitState> traitView;
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int UpgradeCount { get; private set; }
        public int Level { get { return 1 + UpgradeCount; } }
        public IReadOnlyList<SkillState> Skills { get { return skillView; } }
        public IReadOnlyList<TraitState> Traits { get { return traitView; } }
        public float DamageMultiplier { get { return 1 + Bonus(HeroStat.Damage); } }
        public float HasteMultiplier { get { return 1 + Bonus(HeroStat.AttackSpeed); } }
        public float AbilityMultiplier { get { return SkillPowerMultiplier; } }
        public float Damage { get { return StatValue(HeroStat.Damage); } }
        public float MaxHealth { get { return StatValue(HeroStat.MaxHealth); } }
        public float Armor { get { return StatValue(HeroStat.Armor); } }
        public float MoveSpeed { get { return StatValue(HeroStat.MoveSpeed); } }
        public float AttackInterval { get { return StatValue(HeroStat.AttackSpeed); } }
        public float Range { get { return StatValue(HeroStat.Range); } }
        public float CritChance { get { return StatValue(HeroStat.CritChance); } }
        public float LifeSteal { get { return StatValue(HeroStat.LifeSteal); } }
        public float CooldownMultiplier { get { return StatValue(HeroStat.Cooldown); } }
        public float SkillPowerMultiplier { get { return StatValue(HeroStat.SkillPower); } }
        public int ProjectileBonus { get { return (int)StatValue(HeroStat.Projectiles); } }
        public float Regeneration { get { return StatValue(HeroStat.Regeneration); } }
        public float DodgeCooldownMultiplier { get { return StatValue(HeroStat.DodgeCooldown); } }
        public float AreaMultiplier { get { return StatValue(HeroStat.Area); } }
        public float HealingMultiplier { get { return StatValue(HeroStat.Healing); } }
        internal HeroState(string id, string name, float damage, float health, float armor, float moveSpeed, float interval, float range)
        {
            Id = id; Name = name; baseDamage = damage; baseHealth = health; baseArmor = armor;
            baseMoveSpeed = moveSpeed; baseInterval = interval; baseRange = range;
            skillView = skills.AsReadOnly(); traitView = traits.AsReadOnly();
        }
        private float Bonus(HeroStat stat)
        { float value = 0; foreach (var trait in traits) if (trait.Definition.Stat == stat) value += trait.Value; return value; }
        private float StatValue(HeroStat stat, float additionalBonus = 0)
        {
            float bonus = Bonus(stat) + additionalBonus;
            switch (stat)
            {
                case HeroStat.Damage: return baseDamage * (1 + bonus);
                case HeroStat.MaxHealth: return baseHealth * (1 + bonus);
                case HeroStat.Armor: return baseArmor + bonus;
                case HeroStat.MoveSpeed: return baseMoveSpeed * (1 + bonus);
                case HeroStat.AttackSpeed: return baseInterval / (1 + bonus);
                case HeroStat.Range: return baseRange * (1 + bonus);
                case HeroStat.CritChance: return Math.Min(.8f, bonus);
                case HeroStat.LifeSteal: return Math.Min(.6f, bonus);
                case HeroStat.Cooldown:
                case HeroStat.DodgeCooldown: return Math.Max(.25f, 1 - bonus);
                case HeroStat.SkillPower:
                case HeroStat.Area:
                case HeroStat.Healing: return 1 + bonus;
                case HeroStat.Projectiles: return (int)bonus;
                default: return bonus;
            }
        }
        internal bool CanImproveTrait(AugmentDefinition definition)
        { return definition.OwnerId == Id && definition.Kind == RewardKind.Trait && Math.Abs(StatValue(definition.Stat) - StatValue(definition.Stat, definition.Value)) > .000001f; }
        public SkillState GetSkill(string id) { return skills.Find(s => s.Definition.Id == id); }
        public TraitState GetTrait(string id) { return traits.Find(t => t.Definition.Id == id); }
        internal bool Apply(AugmentDefinition definition)
        {
            if (definition.OwnerId != Id) return false;
            if (definition.Kind == RewardKind.Skill)
            {
                var skill = GetSkill(definition.Id);
                if (skill != null && skill.Level >= 3) return false;
                if (skill == null) skills.Add(new SkillState(definition, this)); else skill.Upgrade();
            }
            else
            {
                var trait = GetTrait(definition.Id);
                if (trait == null) traits.Add(new TraitState(definition, this)); else trait.Upgrade();
            }
            UpgradeCount++;
            return true;
        }
        public string StatText(HeroStat stat) { return StatText(stat, 0); }
        internal string StatText(HeroStat stat, float additionalBonus)
        {
            float value = StatValue(stat, additionalBonus);
            switch (stat)
            {
                case HeroStat.AttackSpeed: return AugmentDefinition.Number(value) + "초 간격";
                case HeroStat.CritChance:
                case HeroStat.LifeSteal: return AugmentDefinition.Number(value * 100) + "%";
                case HeroStat.Cooldown:
                case HeroStat.DodgeCooldown: return AugmentDefinition.Number((1 - value) * 100) + "% 감소";
                case HeroStat.SkillPower:
                case HeroStat.Area:
                case HeroStat.Healing: return AugmentDefinition.Number(value) + "배";
                case HeroStat.Projectiles: return "+" + (int)value;
                case HeroStat.Regeneration: return AugmentDefinition.Number(value) + " HP/s";
                default: return AugmentDefinition.Number(value);
            }
        }
    }

    public sealed class CompanionState : HeroState
    {
        public CompanionDefinition Definition { get; private set; }
        public float HealAmount { get { return Definition.HealAmount * HealingMultiplier * SkillPowerMultiplier; } }
        internal CompanionState(CompanionDefinition definition) : base(definition.Id, definition.Name, definition.Damage,
            definition.MaxHealth, definition.Armor, definition.MoveSpeed, definition.AttackInterval, definition.Range) { Definition = definition; }
    }

    public sealed class RewardOffer
    {
        public long Id { get; private set; }
        public int Slot { get; private set; }
        public RewardTier Tier { get; private set; }
        public RewardKind Kind { get; private set; }
        public string CompanionId { get; private set; }
        public string OwnerId { get { return CompanionId; } }
        public string AugmentId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public int NextLevel { get; private set; }
        public int Level { get { return NextLevel; } }
        public bool IsUpgrade { get { return NextLevel > 1; } }
        public float Cooldown { get; private set; }
        public UpgradeStat Stat { get; private set; }
        public float UpgradeValue { get; private set; }
        internal RewardOffer(long id, int slot, CompanionDefinition recruit)
        {
            Id = id; Slot = slot; Tier = recruit.Tier; Kind = RewardKind.Recruit;
            CompanionId = recruit.Id; AugmentId = "recruit:" + recruit.Id; NextLevel = 1;
            Title = recruit.Name + " 영입"; Description = recruit.Title + " · " + recruit.Description;
        }
        internal RewardOffer(long id, int slot, OathAugmentDefinition definition, string companionId)
        {
            Id = id; Slot = slot; Tier = RewardTier.Gold; Kind = RewardKind.Oath;
            CompanionId = companionId; AugmentId = definition.Id; NextLevel = 1;
            Title = definition.Name; Description = definition.Description;
        }
        internal RewardOffer(long id, int slot, AugmentDefinition definition, HeroState owner)
        {
            Id = id; Slot = slot; Tier = definition.Tier; Kind = definition.Kind;
            CompanionId = owner.Id; AugmentId = definition.Id; UpgradeValue = definition.Value;
            Stat = definition.Stat == HeroStat.Damage ? UpgradeStat.Damage : definition.Stat == HeroStat.AttackSpeed ? UpgradeStat.Haste : UpgradeStat.Ability;
            if (Kind == RewardKind.Skill)
            {
                var skill = owner.GetSkill(definition.Id);
                NextLevel = skill == null ? 1 : skill.Level + 1;
                var preview = skill ?? new SkillState(definition, owner);
                Cooldown = preview.CooldownAt(NextLevel);
                Title = owner.Name + " · " + definition.Name + (NextLevel == 1 ? " 습득" : " Lv." + NextLevel + " 강화");
                Description = preview.Describe(NextLevel);
            }
            else
            {
                var trait = owner.GetTrait(definition.Id);
                NextLevel = trait == null ? 1 : trait.Level + 1;
                Title = owner.Name + " · " + definition.Name + (NextLevel > 1 ? " " + NextLevel + "단계" : "");
                Description = definition.Description + "\n현재 " + owner.StatText(definition.Stat) + " → 선택 후 " + owner.StatText(definition.Stat, definition.Value);
            }
        }
    }

    public sealed class TowerRun
    {
        public const int MaxCompanions = 3, RewardInterval = 1, OfferSlots = 3;
        private readonly Random random;
        private readonly Random themeRandom;
        private readonly List<CompanionState> companions = new List<CompanionState>();
        private readonly List<RewardOffer> offers = new List<RewardOffer>();
        private readonly bool[] rerolled = new bool[OfferSlots];
        private readonly ReadOnlyCollection<CompanionState> companionView;
        private readonly ReadOnlyCollection<RewardOffer> offerView;
        private long nextOfferId = 1;
        public int Floor { get; private set; }
        public int ClearedFloors { get; private set; }
        public int MaxFloor { get; private set; }
        public TowerThemeDefinition Theme { get; private set; }
        public string SelectedMythId { get; private set; }
        public string RunId { get; private set; }
        public bool IsFloorCleared { get; private set; }
        public bool IsRewardPending { get; private set; }
        public bool IsComplete { get { return IsFloorCleared && Floor >= MaxFloor && !IsRewardPending; } }
        public HeroState Player { get; private set; }
        public OathState Oath { get; private set; }
        public bool IsOathTrial { get { return Oath != null; } }
        public IReadOnlyList<CompanionState> Companions { get { return companionView; } }
        public IReadOnlyList<RewardOffer> Offers { get { return offerView; } }
        public static readonly IReadOnlyList<CompanionDefinition> Definitions = TowerAugmentCatalog.Companions;
        public static readonly IReadOnlyList<AugmentDefinition> Augments = TowerAugmentCatalog.Augments;
        public TowerRun(int seed = 1, int maxFloor = 25, string mythId = TowerMythCatalog.StarterMythId, string oathCompanionId = null)
            : this(new Random(seed), maxFloor, mythId, new Random(unchecked(seed * 397 ^ 0x51ED270B)), oathCompanionId) { }
        public TowerRun(Random random, int maxFloor = 25, string mythId = TowerMythCatalog.StarterMythId)
            : this(random, maxFloor, mythId, new Random(random == null ? 0 : random.Next())) { }
        private TowerRun(Random random, int maxFloor, string mythId, Random themeRandom, string oathCompanionId = null)
        {
            if (random == null) throw new ArgumentNullException("random");
            if (maxFloor < 1) throw new ArgumentOutOfRangeException("maxFloor");
            if (TowerMythCatalog.Get(mythId) == null) throw new ArgumentException("Unknown myth: " + mythId, "mythId");
            this.random = random; MaxFloor = maxFloor; Floor = 1;
            this.themeRandom = themeRandom; Theme = TowerThemeCatalog.Roll(themeRandom);
            SelectedMythId = mythId; RunId = Guid.NewGuid().ToString("N");
            Player = new HeroState("player", "플레이어", 28, 160, 0, 6, .43f, 2.6f);
            companionView = companions.AsReadOnly(); offerView = offers.AsReadOnly();
            if (oathCompanionId != null)
            {
                Oath = new OathState(oathCompanionId); MaxFloor = 10; SelectedMythId = TowerMythCatalog.StarterMythId;
                companions.Add(new CompanionState(GetDefinition(oathCompanionId)));
            }
        }
        public static CompanionDefinition GetDefinition(string id)
        { for (int i = 0; i < Definitions.Count; i++) if (Definitions[i].Id == id) return Definitions[i]; return null; }
        public static AugmentDefinition GetAugment(string id)
        { for (int i = 0; i < Augments.Count; i++) if (Augments[i].Id == id) return Augments[i]; return null; }
        public CompanionState GetCompanion(string id) { return companions.Find(c => c.Id == id); }
        public HeroState GetHero(string id) { return id == "player" ? Player : GetCompanion(id); }
        public bool ClearFloor()
        {
            if (IsFloorCleared || IsRewardPending || IsComplete) return false;
            IsFloorCleared = true; ClearedFloors++; IsRewardPending = true;
            if (IsOathTrial && Floor == MaxFloor) { IsRewardPending = false; offers.Clear(); return true; }
            Array.Clear(rerolled, 0, rerolled.Length); GenerateOffers(); return true;
        }
        public bool AdvanceFloor()
        {
            if (!IsFloorCleared || IsRewardPending || IsComplete) return false;
            Floor++; IsFloorCleared = false;
            if ((Floor - 1) % TowerThemeCatalog.FloorsPerTheme == 0)
                Theme = TowerThemeCatalog.Roll(themeRandom, Theme.Id);
            return true;
        }
        public bool CanReroll(int slot) { return IsRewardPending && slot >= 0 && slot < OfferSlots && !rerolled[slot]; }
        public bool Reroll(int slot)
        {
            if (!CanReroll(slot)) return false;
            var previous = offers[slot]; rerolled[slot] = true; offers[slot] = CreateOffer(slot, previous.AugmentId); return true;
        }
        public bool ChooseOffer(long offerId)
        {
            if (!IsRewardPending) return false;
            var offer = offers.Find(o => o.Id == offerId);
            if (offer == null) return false;
            if (offer.Kind == RewardKind.Recruit)
            {
                var definition = GetDefinition(offer.CompanionId);
                if (companions.Count >= MaxCompanions || GetCompanion(offer.CompanionId) != null || definition == null || definition.MythId != SelectedMythId) return false;
                companions.Add(new CompanionState(definition));
            }
            else if (offer.Kind == RewardKind.Oath)
            {
                if (Oath == null || !Oath.Acquire(offer.AugmentId)) return false;
            }
            else
            {
                var hero = GetHero(offer.CompanionId);
                var augment = GetAugment(offer.AugmentId);
                if (hero == null || augment == null || !hero.Apply(augment)) return false;
            }
            IsRewardPending = false; offers.Clear(); return true;
        }
        public bool DismissCompanion(string id)
        {
            var companion = GetCompanion(id);
            if (companion == null || (Oath != null && Oath.CompanionId == id)) return false;
            companions.Remove(companion);
            if (IsRewardPending) GenerateOffers();
            return true;
        }
        private void GenerateOffers()
        { offers.Clear(); for (int slot = 0; slot < OfferSlots; slot++) offers.Add(CreateOffer(slot, null)); }
        private sealed class Candidate
        {
            public CompanionDefinition Recruit;
            public AugmentDefinition Augment;
            public string Id { get { return Recruit != null ? "recruit:" + Recruit.Id : Augment.Id; } }
            public int Weight { get { return Recruit != null ? 12 : Augment.Kind == RewardKind.Skill ? Augment.OwnerId == "player" ? 6 : 10 : Augment.OwnerId == "player" ? 4 : 6; } }
        }
        private RewardOffer CreateOffer(int slot, string previousAugment)
        {
            if (Oath != null && slot == 0)
            {
                var available = new List<OathAugmentDefinition>();
                foreach (var a in OathState.Augments)
                    if (Oath.CanAcquire(a) && a.Id != previousAugment) available.Add(a);
                if (available.Count > 0)
                    return new RewardOffer(nextOfferId++, slot, available[random.Next(available.Count)], Oath.CompanionId);
            }
            double roll = random.NextDouble();
            RewardTier tier = roll < .4 ? RewardTier.Silver : roll < .8 ? RewardTier.Gold : RewardTier.Diamond;
            var candidates = new List<Candidate>();
            if (companions.Count < MaxCompanions)
                foreach (var definition in Definitions)
                    if (definition.MythId == SelectedMythId && definition.Tier == tier && GetCompanion(definition.Id) == null &&
                        (!IsOathTrial || OathState.Supports(definition.Id))) candidates.Add(new Candidate { Recruit = definition });
            foreach (var definition in Augments)
            {
                if (definition.Tier != tier) continue;
                var owner = GetHero(definition.OwnerId);
                if (owner == null) continue;
                var skill = owner.GetSkill(definition.Id);
                if (definition.Kind == RewardKind.Skill && skill != null && skill.Level >= 3) continue;
                candidates.Add(new Candidate { Augment = definition });
            }
            candidates.RemoveAll(c => c.Id == previousAugment || offers.Exists(o => o.Slot != slot && o.AugmentId == c.Id));
            // Prefer useful growth; extreme custom-length runs can exhaust a tier's distinct useful identities.
            var useful = candidates.FindAll(c => c.Recruit != null || c.Augment.Kind == RewardKind.Skill || GetHero(c.Augment.OwnerId).CanImproveTrait(c.Augment));
            if (useful.Count > 0) candidates = useful;
            int total = 0; foreach (var candidate in candidates) total += candidate.Weight;
            int pick = random.Next(total);
            Candidate selected = candidates[0];
            foreach (var candidate in candidates) { pick -= candidate.Weight; if (pick < 0) { selected = candidate; break; } }
            return selected.Recruit != null ? new RewardOffer(nextOfferId++, slot, selected.Recruit) :
                new RewardOffer(nextOfferId++, slot, selected.Augment, GetHero(selected.Augment.OwnerId));
        }
    }
}
