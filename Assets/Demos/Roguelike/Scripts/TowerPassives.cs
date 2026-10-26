using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Carries out the party's passives in the fight. The counters, stacks and clocks live in TowerPassiveState;
    // this side finds the targets, deals the damage and shows each effect.
    public sealed partial class TowerGame
    {
        private sealed class Affliction { public Fighter enemy; public HeroState owner; public PassiveKind kind; public float remaining, dps, tick; public int stacks; }
        private sealed class PassiveBlast { public Vector2 pos; public float delay, damage, radius; public HeroState owner; public SkillEffect effect; public Color color; }
        private sealed class SkillEcho { public HeroState owner; public SkillState skill; public Fighter source, target; public float delay, scale; }
        private readonly List<Affliction> afflictions = new List<Affliction>();
        private readonly List<PassiveBlast> passiveBlasts = new List<PassiveBlast>(), readyBlasts = new List<PassiveBlast>();
        private readonly List<SkillEcho> skillEchoes = new List<SkillEcho>();
        private readonly Dictionary<Fighter, float> weakenedUntil = new Dictionary<Fighter, float>();
        private readonly HashSet<Fighter> sureshotSeen = new HashSet<Fighter>();
        private System.Random passiveRandom = new System.Random(1);
        private bool forcedCritical, judging, overflowClaimed;
        private float veilRemaining, herbRemaining, herbClock;
        private CompanionPassive herb;
        /// <summary>How often each passive has fired this run; validation reads it.</summary>
        internal readonly Dictionary<PassiveKind, int> PassiveFired = new Dictionary<PassiveKind, int>();
        internal int PassiveFireCount(PassiveKind kind) { int count; return PassiveFired.TryGetValue(kind, out count) ? count : 0; }
        internal int Afflictions { get { return afflictions.Count; } }

        private void Fired(PassiveKind kind) { int count; PassiveFired.TryGetValue(kind, out count); PassiveFired[kind] = count + 1; }
        private static Color CompanionTint(CompanionState state)
        { Color color; return state != null && ColorUtility.TryParseHtmlString("#" + state.Definition.TintHex, out color) ? color : TowerWorld.Gold; }
        /// <summary>Counts the passive and floats its name over the companion who holds it.</summary>
        private void PassiveCue(PassiveKind kind, string text = null, Vector2? at = null)
        {
            Fired(kind);
            if (text == null) return;
            var holder = run.Passives.Holder(kind); var actor = holder == null ? null : ActorFor(holder.Id);
            FloatText(at ?? ((actor ?? hero).pos + Vector2.up * 2.5f), text, CompanionTint(holder));
        }

        private void ResetPassiveFloor()
        {
            afflictions.Clear(); passiveBlasts.Clear(); skillEchoes.Clear(); weakenedUntil.Clear(); sureshotSeen.Clear();
            forcedCritical = judging = false; herbRemaining = herbClock = 0; herb = null; EndVeil();
            passiveRandom = new System.Random(unchecked(seed * 131 ^ run.Floor * 977));
            run.Passives.OnFloorStart();
        }
        private void OnPassiveFloorCleared()
        {
            afflictions.Clear(); passiveBlasts.Clear(); skillEchoes.Clear();
            run.Passives.OnFloorCleared();
            var cauldron = run.Passives.Active(PassiveKind.Cauldron);
            if (cauldron != null && hero.hp > 0 && hero.hp < PlayerMaxHealth)
            {
                HealPlayer(PlayerMaxHealth * cauldron.Value, true); PassiveCue(PassiveKind.Cauldron, cauldron.Name, hero.pos + Vector2.up * 2.8f);
                if (vfx != null) vfx.Impact(SkillEffect.HealPulse, hero.pos + Vector2.up, 1.6f, CompanionTint(run.Passives.Holder(PassiveKind.Cauldron)));
            }
        }

        private void TickPassives(float dt)
        {
            var passives = run.Passives; passives.Tick(dt);
            if (veilRemaining > 0) { veilRemaining -= dt; if (veilRemaining <= 0) EndVeil(); }
            if (herbRemaining > 0)
            {
                herbRemaining -= dt; herbClock += dt;
                if (herbClock >= 1) { herbClock -= 1; if (herb != null) HealPlayer(herb.Value, false); }
            }
            TickAfflictions(dt); TickBlasts(dt); TickEchoes(dt);
            if (run.Companions.Count == 0) return;
            bool fighting = !village && !run.IsFloorCleared && enemies.Count > 0;
            for (int i = 0; i < run.Companions.Count; i++)
            {
                var companion = run.Companions[i]; var passive = companion.Definition.Passive;
                if (!passive.IsPeriodic) continue;
                var actor = ActorFor(companion.Id);
                bool due = passives.PeriodicRemaining(passive.Kind) <= dt;
                Fighter target = due && fighting && actor != null ? PeriodicTarget(passive, companion, actor) : null;
                bool armed = target != null || due && fighting && actor != null && PeriodicNeedsNoTarget(passive.Kind);
                if (passives.Periodic(passive.Kind, dt, armed)) FirePeriodic(passive, companion, actor, target);
            }
        }
        private static bool PeriodicNeedsNoTarget(PassiveKind kind)
        { return kind == PassiveKind.SkyStone || kind == PassiveKind.Melody || kind == PassiveKind.ThreeSeals; }
        private Fighter PeriodicTarget(CompanionPassive passive, CompanionState owner, Fighter actor)
        {
            switch (passive.Kind)
            {
                case PassiveKind.Gungnir:
                    Fighter strongest = null;
                    foreach (var e in enemies) if (CanTarget(e, owner) && Vector2.Distance(e.pos, actor.pos) < passive.Radius && (strongest == null || e.hp > strongest.hp)) strongest = e;
                    return strongest;
                case PassiveKind.SunFlare: case PassiveKind.Roar: return ClosestEnemy(actor.pos, passive.Radius, owner);
                case PassiveKind.Quake:
                    Fighter grounded = null; float best = 13;
                    foreach (var e in enemies) { float d = Vector2.Distance(e.pos, actor.pos); if (CanTarget(e, owner) && !e.airborne && d < best) { best = d; grounded = e; } }
                    return grounded;
                case PassiveKind.PearlWave:
                    return ValidAllyTarget(actor.combatTarget, owner) ? actor.combatTarget : ClosestEnemy(actor.pos, 13, owner);
                case PassiveKind.Foresight:
                    // Any enemy stands in for the bullet; the bullet itself is chosen when the shot fires.
                    return IncomingBullet(passive.Radius) != null ? hero : null;
                default: return null;
            }
        }
        private Shot IncomingBullet(float radius)
        {
            Shot nearest = null; float best = radius * radius; Vector2 body = hero.pos + Vector2.up * .9f;
            foreach (var shot in shots)
            {
                if (!shot.hostile || shot.life <= 0) continue;
                Vector2 offset = body - shot.pos; float distance = offset.sqrMagnitude;
                if (distance < best && Vector2.Dot(offset, shot.velocity) > 0) { best = distance; nearest = shot; }
            }
            return nearest;
        }
        private void FirePeriodic(CompanionPassive passive, CompanionState owner, Fighter actor, Fighter target)
        {
            Color color = CompanionTint(owner); Vector2 origin = actor.pos + Vector2.up * .9f;
            switch (passive.Kind)
            {
                case PassiveKind.Gungnir:
                    if (vfx != null) { vfx.Trail(origin + Vector2.up * .4f, target.pos + Vector2.up, SkillEffect.PierceShot, color); vfx.Impact(SkillEffect.PierceShot, target.pos + Vector2.up, .9f, color); }
                    StrikeEnemy(target, owner.Damage * passive.Value, owner, Mathf.Sign(target.pos.x - actor.pos.x) * .3f);
                    PassiveCue(passive.Kind, passive.Name); Sound(chimeSound, .3f); break;
                case PassiveKind.SunFlare:
                    if (vfx != null) vfx.Impact(SkillEffect.Fireball, origin, passive.Radius, color);
                    foreach (var e in enemies.ToArray())
                        if (CanTarget(e, owner) && Vector2.Distance(e.pos, actor.pos) < passive.Radius)
                        { StrikeEnemy(e, owner.Damage * passive.Value, owner, 0); if (CanTarget(e, owner)) Afflict(e, owner, PassiveKind.SunFlare, owner.Damage * passive.Secondary, passive.Duration, 1); }
                    PassiveCue(passive.Kind, passive.Name); break;
                case PassiveKind.SkyStone:
                    float cap = PlayerMaxHealth * passive.Secondary;
                    if (playerShield < cap) playerShield = Mathf.Min(cap, playerShield + PlayerMaxHealth * passive.Value);
                    shieldTime = Mathf.Max(shieldTime, passive.Interval + 1);
                    if (vfx != null) vfx.Cast(SkillEffect.ShieldPulse, hero.pos + Vector2.up, 1.1f, color);
                    PassiveCue(passive.Kind, passive.Name, hero.pos + Vector2.up * 2.6f); break;
                case PassiveKind.ThreeSeals: CastSeal(passive, owner, actor, color); break;
                case PassiveKind.Quake:
                    Vector2 center = target.pos;
                    if (vfx != null) { vfx.Impact(SkillEffect.Shockwave, center + Vector2.up * .3f, passive.Radius, color); vfx.Dust(center, 0, 1.2f); }
                    foreach (var e in enemies.ToArray())
                        if (CanTarget(e, owner) && !e.airborne && Vector2.Distance(e.pos, center) < passive.Radius)
                        { e.slowTime = Mathf.Max(e.slowTime, passive.Duration); StrikeEnemy(e, owner.Damage * passive.Value, owner, 0); }
                    Shake(.1f, .18f); PassiveCue(passive.Kind, passive.Name); break;
                case PassiveKind.Foresight:
                    var bullet = IncomingBullet(passive.Radius); if (bullet == null) break;
                    if (vfx != null) { vfx.Trail(origin, bullet.pos, SkillEffect.PierceShot, color); vfx.Hit(bullet.pos, color); }
                    Destroy(bullet.root); shots.Remove(bullet);
                    PassiveCue(passive.Kind, "예지", bullet.pos + Vector2.up * .4f); break;
                case PassiveKind.Melody:
                    if (vfx != null) { vfx.Cast(SkillEffect.HealPulse, origin, 1.2f, color); vfx.ImpactRing(hero.pos + Vector2.up, 1.6f, color); }
                    PassiveCue(passive.Kind, passive.Name); break;
                case PassiveKind.Roar:
                    if (vfx != null) vfx.ImpactRing(origin, passive.Radius, color);
                    foreach (var e in enemies) if (CanTarget(e, owner) && Vector2.Distance(e.pos, actor.pos) < passive.Radius) e.stunTime = Mathf.Max(e.stunTime, passive.Duration);
                    Shake(.08f, .15f); PassiveCue(passive.Kind, passive.Name); break;
                case PassiveKind.PearlWave:
                    Fire(origin, target.pos + Vector2.up * .7f, 14, owner.Damage * passive.Value, false, color, 0, owner, 99, passive.Duration);
                    StyleLastShot(SkillEffect.Shockwave); LimitLastShotRange(18, 14);
                    PassiveCue(passive.Kind, passive.Name); break;
            }
        }
        private void CastSeal(CompanionPassive passive, CompanionState owner, Fighter actor, Color color)
        {
            switch (run.Passives.NextSeal())
            {
                case 0:
                    if (vfx != null) vfx.Impact(SkillEffect.Shockwave, hero.pos + Vector2.up, passive.Radius, color);
                    foreach (var e in enemies)
                        if (CanTarget(e, owner) && Vector2.Distance(e.pos, hero.pos) < passive.Radius)
                        { e.slowTime = Mathf.Max(e.slowTime, passive.Duration); e.pos.x = Mathf.Clamp(e.pos.x + Mathf.Sign(e.pos.x - hero.pos.x) * 1.2f, TowerWorld.Left, TowerWorld.Right); }
                    PassiveCue(passive.Kind, "풍백의 바람"); break;
                case 1:
                    HealPlayer(PlayerMaxHealth * passive.Secondary, true);
                    if (vfx != null) vfx.Impact(SkillEffect.HealPulse, hero.pos + Vector2.up, 1.5f, color);
                    PassiveCue(passive.Kind, "우사의 비"); break;
                default:
                    var targets = enemies.FindAll(e => CanTarget(e, owner));
                    targets.Sort((a, b) => Vector2.Distance(a.pos, actor.pos).CompareTo(Vector2.Distance(b.pos, actor.pos)));
                    for (int i = 0; i < Mathf.Min(passive.Stacks, targets.Count); i++)
                    {
                        if (vfx != null) { vfx.Trail(targets[i].pos + Vector2.up * 7, targets[i].pos + Vector2.up, SkillEffect.ChainLightning, color); vfx.Impact(SkillEffect.ChainLightning, targets[i].pos + Vector2.up, .9f, color); }
                        StrikeEnemy(targets[i], owner.Damage * passive.Value, owner, 0);
                    }
                    PassiveCue(passive.Kind, "운사의 낙뢰"); break;
            }
        }

        /// <summary>A vanguard's basic swing landing on <paramref name="target"/>.</summary>
        private void AllyStrike(Fighter actor, CompanionState state, Fighter target)
        {
            var passive = state.Definition.Passive;
            bool empowered = run.Passives.OnBasicAttack(state);
            if (passive.Kind == PassiveKind.Momentum) Fired(passive.Kind);
            float damage = state.Damage * (empowered && passive.Kind == PassiveKind.Mjolnir ? passive.Value : 1);
            StrikeEnemy(target, damage, state, actor.facing * .22f);
            if (passive.Kind == PassiveKind.Ruyi)
            {
                // The staff stretches through everyone ahead, not just the one it was swung at.
                float reach = state.Range * passive.Secondary; bool swept = false;
                foreach (var e in enemies.ToArray())
                    if (e != target && CanTarget(e, state) && (e.pos.x - actor.pos.x) * actor.facing > -.4f && Mathf.Abs(e.pos.x - actor.pos.x) < reach && Mathf.Abs(e.pos.y - actor.pos.y) < 1.8f)
                    { StrikeEnemy(e, state.Damage * passive.Value, state, actor.facing * .18f); swept = true; }
                if (swept) { Fired(passive.Kind); if (vfx != null) vfx.Trail(actor.pos + Vector2.up * .8f, actor.pos + new Vector2(actor.facing * reach, .8f), SkillEffect.Shockwave, CompanionTint(state)); }
            }
            AfterBasicHit(state, target, empowered, null);
        }
        /// <summary>Marks the shot a ranged companion just loosed as its basic attack.</summary>
        private void TagAllyShot(Fighter actor, CompanionState state, Fighter target, Color color)
        {
            if (shots.Count == 0) return;
            var shot = shots[shots.Count - 1]; var passive = state.Definition.Passive;
            shot.basic = true; shot.empowered = run.Passives.OnBasicAttack(state);
            switch (passive.Kind)
            {
                case PassiveKind.Discus: shot.pierce += passive.Stacks; break;
                case PassiveKind.Insight:
                    if (!shot.empowered) break;
                    shot.damage *= passive.Value; shot.pierce = 99; shot.forceCrit = true; StyleLastShot(SkillEffect.PierceShot);
                    PassiveCue(passive.Kind, passive.Name); break;
                case PassiveKind.SplitShot:
                    if (!shot.empowered) break;
                    var others = enemies.FindAll(e => e != target && CanTarget(e, state) && Vector2.Distance(e.pos, actor.pos) < passive.Radius);
                    others.Sort((a, b) => Vector2.Distance(a.pos, target.pos).CompareTo(Vector2.Distance(b.pos, target.pos)));
                    for (int i = 0; i < Mathf.Min(passive.Stacks, others.Count); i++)
                    { Fire(actor.pos + Vector2.up * .78f, others[i].pos + Vector2.up * .7f, 11, state.Damage * passive.Value, false, color, 0, state); StyleLastShot(SkillEffect.MultiShot); }
                    if (others.Count > 0) PassiveCue(passive.Kind, passive.Name);
                    break;
            }
        }
        private void AfterShotHit(Shot shot, Fighter enemy)
        {
            var state = shot.owner as CompanionState;
            if (state == null || !IsCurrentHero(state)) return;
            AfterBasicHit(state, enemy, shot.empowered, shot);
            // A piercing shot empowers only its first contact.
            shot.empowered = false;
        }
        private void AfterBasicHit(CompanionState state, Fighter target, bool empowered, Shot shot)
        {
            var p = state.Definition.Passive; Color color = CompanionTint(state);
            bool alive = CanTarget(target, state); Vector2 at = target.pos;
            switch (p.Kind)
            {
                case PassiveKind.Ignite: if (alive) Afflict(target, state, p.Kind, state.Damage * p.Value, p.Duration, 1); break;
                case PassiveKind.GaeBolg: if (alive) Afflict(target, state, p.Kind, state.Damage * p.Value, p.Duration, p.Stacks); break;
                case PassiveKind.Tide: if (alive) { target.slowTime = Mathf.Max(target.slowTime, p.Duration); Fired(p.Kind); } break;
                case PassiveKind.ThirdEye:
                    if (!alive) break;
                    weakenedUntil[target] = elapsed + p.Duration; Fired(p.Kind);
                    if (vfx != null) vfx.ImpactRing(target.pos + Vector2.up * 1.7f, .35f, color); break;
                case PassiveKind.Mjolnir:
                    if (!empowered) break;
                    if (alive) target.stunTime = Mathf.Max(target.stunTime, p.Duration);
                    if (vfx != null) vfx.Impact(SkillEffect.ChainLightning, at + Vector2.up, 1.2f, color);
                    Shake(.06f, .1f); PassiveCue(p.Kind, "기절", at + Vector2.up * 2.4f); break;
                case PassiveKind.ChainStrike:
                    if (!empowered) break;
                    var chained = enemies.FindAll(e => e != target && CanTarget(e, state) && Vector2.Distance(e.pos, at) < p.Radius);
                    chained.Sort((a, b) => Vector2.Distance(a.pos, at).CompareTo(Vector2.Distance(b.pos, at)));
                    Vector2 from = at + Vector2.up;
                    for (int i = 0; i < Mathf.Min(p.Stacks, chained.Count); i++)
                    { if (vfx != null) vfx.Trail(from, chained[i].pos + Vector2.up, SkillEffect.ChainLightning, color); from = chained[i].pos + Vector2.up; StrikeEnemy(chained[i], state.Damage * p.Value, state, 0); }
                    if (chained.Count > 0) PassiveCue(p.Kind); break;
                case PassiveKind.GroundSlam:
                    if (!empowered) break;
                    if (vfx != null) { vfx.Impact(SkillEffect.Shockwave, at + Vector2.up * .3f, p.Radius, color); vfx.Dust(at, 0, 1); }
                    foreach (var e in enemies.ToArray()) if (CanTarget(e, state) && Vector2.Distance(e.pos, at) < p.Radius) StrikeEnemy(e, state.Damage * p.Value, state, Mathf.Sign(e.pos.x - at.x) * .3f);
                    Shake(.07f, .12f); PassiveCue(p.Kind, p.Name, at + Vector2.up * 2.4f); break;
                case PassiveKind.Cleave:
                    bool cleaved = false;
                    foreach (var e in enemies.ToArray()) if (e != target && CanTarget(e, state) && Vector2.Distance(e.pos, at) < p.Radius) { StrikeEnemy(e, state.Damage * p.Value, state, Mathf.Sign(e.pos.x - at.x) * .2f); cleaved = true; }
                    if (cleaved) { Fired(p.Kind); SlashArc(at + Vector2.up, target.pos.x >= hero.pos.x ? 1 : -1, color); } break;
                case PassiveKind.Ricochet:
                    var struck = new HashSet<Fighter> { target }; Vector2 bounce = at;
                    for (int i = 0; i < p.Stacks; i++)
                    {
                        Fighter next = null; float best = p.Radius;
                        foreach (var e in enemies) { float d = Vector2.Distance(e.pos, bounce); if (!struck.Contains(e) && CanTarget(e, state) && d < best) { best = d; next = e; } }
                        if (next == null) break;
                        if (vfx != null) vfx.Trail(bounce + Vector2.up * .7f, next.pos + Vector2.up * .7f, SkillEffect.PierceShot, color);
                        struck.Add(next); bounce = next.pos; StrikeEnemy(next, state.Damage * p.Value, state, 0); Fired(p.Kind);
                    }
                    break;
                case PassiveKind.Discus: if (shot != null) { shot.damage *= 1 + p.Value; Fired(p.Kind); } break;
            }
        }

        private void Afflict(Fighter enemy, HeroState owner, PassiveKind kind, float dps, float duration, int maxStacks)
        {
            Fired(kind);
            foreach (var a in afflictions)
                if (a.enemy == enemy && a.kind == kind)
                { a.stacks = Mathf.Min(maxStacks, a.stacks + 1); a.remaining = duration; a.dps = Mathf.Max(a.dps, dps); return; }
            afflictions.Add(new Affliction { enemy = enemy, owner = owner, kind = kind, dps = dps, remaining = duration, tick = .5f, stacks = 1 });
        }
        private void TickAfflictions(float dt)
        {
            for (int i = afflictions.Count - 1; i >= 0; i--)
            {
                if (i >= afflictions.Count) continue;
                var a = afflictions[i]; a.remaining -= dt; a.tick -= dt;
                if (!CanTarget(a.enemy, a.owner) || !IsCurrentHero(a.owner) || !enemies.Contains(a.enemy)) { afflictions.Remove(a); continue; }
                if (a.tick <= 0)
                {
                    a.tick += .5f;
                    if (vfx != null) vfx.Hit(a.enemy.pos + Vector2.up * .9f, a.kind == PassiveKind.GaeBolg ? new Color(.86f, .2f, .26f) : new Color(1, .5f, .18f));
                    StrikeEnemy(a.enemy, a.dps * .5f * a.stacks, a.owner, 0);
                }
                if (a.remaining <= 0) afflictions.Remove(a);
            }
        }
        private void QueueBlast(Vector2 at, float delay, float damage, float radius, HeroState owner, SkillEffect effect, Color color)
        { passiveBlasts.Add(new PassiveBlast { pos = at, delay = delay, damage = damage, radius = radius, owner = owner, effect = effect, color = color }); }
        private void TickBlasts(float dt)
        {
            if (passiveBlasts.Count == 0) return;
            readyBlasts.Clear();
            for (int i = passiveBlasts.Count - 1; i >= 0; i--)
            { var blast = passiveBlasts[i]; blast.delay -= dt; if (blast.delay <= 0) { readyBlasts.Add(blast); passiveBlasts.RemoveAt(i); } }
            // A blast that kills queues the next one, so a chain spreads a step per frame.
            foreach (var blast in readyBlasts)
            {
                if (!IsCurrentHero(blast.owner)) continue;
                if (vfx != null) vfx.Impact(blast.effect, blast.pos + Vector2.up * .7f, blast.radius, blast.color);
                foreach (var e in enemies.ToArray())
                    if (CanTarget(e, blast.owner) && Vector2.Distance(e.pos, blast.pos) < blast.radius) StrikeEnemy(e, blast.damage, blast.owner, Mathf.Sign(e.pos.x - blast.pos.x) * .25f);
                Shake(.05f, .1f);
            }
            readyBlasts.Clear();
        }
        private void TickEchoes(float dt)
        {
            for (int i = skillEchoes.Count - 1; i >= 0; i--)
            {
                var echo = skillEchoes[i]; echo.delay -= dt; if (echo.delay > 0) continue;
                skillEchoes.RemoveAt(i);
                if (!IsCurrentHero(echo.owner) || echo.source == null || !echo.source.root || hero.hp <= 0) continue;
                var target = echo.target != null && CanTarget(echo.target, echo.owner) ? echo.target : ClosestEnemy(echo.source.pos, 13, echo.owner);
                if (target == null) continue;
                CastScaledSkill(echo.owner, echo.skill, echo.source, target, echo.scale);
                PassiveCue(PassiveKind.Creation, "창조의 반복", echo.source.pos + Vector2.up * 2.8f);
            }
        }

        // StrikeEnemy consults these for every blow the party lands.
        private float PassiveDamageScale(Fighter enemy, HeroState owner)
        { return owner == null || run.Companions.Count == 0 ? 1 : 1 + run.Passives.DamageBonus(owner, TargetOf(enemy, owner)); }
        private PassiveTarget TargetOf(Fighter enemy, HeroState owner)
        {
            // Only Lugh's long arm reads the distance, so the attacker's actor is looked up just for him.
            var source = owner == null || !run.Passives.Owns(owner, PassiveKind.LongArm) ? null : ActorFor(owner.Id); float until;
            return new PassiveTarget
            {
                HealthFraction = enemy.maxHp > 0 ? enemy.hp / enemy.maxHp : 1, Distance = source == null ? 0 : Vector2.Distance(source.pos, enemy.pos),
                PlayerHealthFraction = PlayerMaxHealth > 0 ? hero.hp / PlayerMaxHealth : 1, Elite = enemy.isElite, Boss = enemy.isBoss,
                Slowed = enemy.slowTime > 0, Marked = weakenedUntil.TryGetValue(enemy, out until) && until > elapsed
            };
        }
        private bool PassiveSureshot(Fighter enemy, HeroState owner)
        {
            if (!run.Passives.Owns(owner, PassiveKind.Sureshot) || !sureshotSeen.Add(enemy)) return false;
            Fired(PassiveKind.Sureshot); return true;
        }
        private void AfterPassiveStrike(Fighter enemy, HeroState owner)
        {
            if (enemy.hp <= 0) { OnPassiveKill(enemy, owner); return; }
            if (judging || run.Companions.Count == 0 || !run.Passives.Judges(TargetOf(enemy, owner))) return;
            judging = true;
            if (vfx != null) vfx.Impact(SkillEffect.LifeDrain, enemy.pos + Vector2.up, 1, CompanionTint(run.Passives.Holder(PassiveKind.Judgment)));
            PassiveCue(PassiveKind.Judgment, "심판", enemy.pos + Vector2.up * 2.4f);
            criticalHit = false; DamageEnemy(enemy, enemy.hp, 0);
            judging = false;
            if (enemy.hp <= 0) OnPassiveKill(enemy, owner);
        }
        private void OnPassiveKill(Fighter enemy, HeroState owner)
        {
            var passives = run.Passives;
            if (run.Companions.Count == 0) return;
            passives.OnKill(); if (passives.Has(PassiveKind.Wrath)) Fired(PassiveKind.Wrath);
            bool great = enemy.isElite || enemy.isBoss; Vector2 at = enemy.pos + Vector2.up * .6f;
            var toll = passives.Active(PassiveKind.SoulToll);
            if (toll != null && hero.hp > 0)
            {
                if (vfx != null) vfx.Trail(enemy.pos + Vector2.up, hero.pos + Vector2.up, SkillEffect.LifeDrain, CompanionTint(passives.Holder(PassiveKind.SoulToll)));
                HealPlayer(great ? toll.Secondary : toll.Value, true); Fired(PassiveKind.SoulToll);
            }
            var shiva = passives.Holder(PassiveKind.Destruction);
            if (shiva != null)
            { var dance = shiva.Definition.Passive; QueueBlast(enemy.pos, .12f, shiva.Damage * dance.Value, dance.Radius, shiva, SkillEffect.Fireball, CompanionTint(shiva)); Fired(PassiveKind.Destruction); }
            var tears = passives.Active(PassiveKind.GoldTears);
            if (tears != null && (great || passiveRandom.NextDouble() < tears.Value)) { DropLoot(great ? LootKind.GoldCoin : LootKind.Coin, at, passiveRandom); Fired(PassiveKind.GoldTears); }
            var harvest = passives.Active(PassiveKind.Harvest);
            if (harvest != null && (great || passiveRandom.NextDouble() < harvest.Value)) { DropLoot(LootKind.Heart, at, passiveRandom); Fired(PassiveKind.Harvest); }
        }

        // HurtHero consults these for every blow the player takes.
        private bool PassiveWardsHit()
        {
            if (run.Companions.Count == 0 || !run.Passives.TryBlock()) return false;
            invulnerable = Mathf.Max(invulnerable, .65f);
            if (vfx != null) vfx.Impact(SkillEffect.ShieldPulse, hero.pos + Vector2.up, 1.1f, CompanionTint(run.Passives.Holder(PassiveKind.Pathfinder)));
            PassiveCue(PassiveKind.Pathfinder, "길잡이의 축복", hero.pos + Vector2.up * 2.2f); Sound(chimeSound, .3f);
            return true;
        }
        private float PassiveDamageTakenScale { get { return run.Companions.Count == 0 ? 1 : run.Passives.DamageTakenScale(PlayerMaxHealth > 0 ? hero.hp / PlayerMaxHealth : 1); } }
        private float PassiveTrapScale { get { return run.Companions.Count == 0 ? 1 : run.Passives.TrapDamageScale; } }
        private void PassiveRebirth()
        {
            if (run.Companions.Count == 0 || !run.Passives.TryRebirth()) return;
            var holder = run.Passives.Holder(PassiveKind.Rebirth); var passive = holder.Definition.Passive;
            hero.hp = Mathf.Max(1, PlayerMaxHealth * passive.Value); hero.hurt = 0; invulnerable = Mathf.Max(invulnerable, 2);
            if (vfx != null) { vfx.Cast(SkillEffect.HealPulse, hero.pos + Vector2.up, 1.4f, TowerWorld.Teal); vfx.ImpactRing(hero.pos + Vector2.up, 2.2f, CompanionTint(holder)); }
            PassiveCue(PassiveKind.Rebirth, "부활", hero.pos + Vector2.up * 2.8f);
            Toast(passive.Name, holder.Name + "의 힘으로 다시 일어섭니다  ·  " + passive.Stacks + "층 뒤 재충전", CompanionTint(holder), 3.2f);
            Shake(.18f, .3f); Sound(chimeSound, .8f);
        }
        private void OnPassiveHurt()
        {
            var passives = run.Passives;
            if (run.Companions.Count == 0) return;
            passives.OnPlayerHurt(); if (passives.Has(PassiveKind.Endure)) Fired(PassiveKind.Endure);
            var aegis = passives.Active(PassiveKind.Aegis);
            if (aegis != null)
            {
                var near = enemies.FindAll(e => CanTarget(e, run.Player) && Vector2.Distance(e.pos, hero.pos) <= aegis.Radius);
                if (near.Count > 0 && passives.TryCooldown(PassiveKind.Aegis))
                {
                    foreach (var e in near) e.stunTime = Mathf.Max(e.stunTime, aegis.Duration);
                    if (vfx != null) vfx.ImpactRing(hero.pos + Vector2.up, aegis.Radius, new Color(.78f, .8f, .74f));
                    PassiveCue(PassiveKind.Aegis, "석화");
                }
            }
            var wings = passives.Active(PassiveKind.Wings);
            if (wings != null && hero.hp > 0 && hero.hp < PlayerMaxHealth * wings.Threshold && passives.TryCooldown(PassiveKind.Wings))
            {
                playerShield = Mathf.Min(PlayerMaxHealth * .65f, playerShield + PlayerMaxHealth * wings.Value); shieldTime = Mathf.Max(shieldTime, wings.Duration);
                if (vfx != null) vfx.Cast(SkillEffect.ShieldPulse, hero.pos + Vector2.up, 1.3f, CompanionTint(passives.Holder(PassiveKind.Wings)));
                PassiveCue(PassiveKind.Wings, wings.Name, hero.pos + Vector2.up * 2.6f);
            }
        }

        // Dodges: the decoy, the veil and the scout's quick reset.
        private void OnPassiveDodge()
        {
            var passives = run.Passives;
            if (run.Companions.Count == 0) return;
            var loki = passives.Holder(PassiveKind.Decoy);
            if (loki != null && passives.TryCooldown(PassiveKind.Decoy))
            {
                var trick = loki.Definition.Passive;
                QueueBlast(hero.pos, trick.Duration, loki.Damage * trick.Value, trick.Radius, loki, SkillEffect.PoisonCloud, CompanionTint(loki));
                if (vfx != null) vfx.Afterimage(hero.root.transform, trick.Duration);
                Fired(PassiveKind.Decoy);
            }
            var veil = passives.Active(PassiveKind.Veil);
            if (veil != null)
            {
                veilRemaining = .25f + veil.Value; invulnerable = Mathf.Max(invulnerable, veilRemaining);
                hero.art.Tint = new Color(1, 1, 1, .4f); Fired(PassiveKind.Veil);
            }
        }
        private void EndVeil() { veilRemaining = 0; if (hero != null && hero.art != null) hero.art.Tint = Color.white; }
        private float PassiveDodgeWindow { get { return run.Companions.Count == 0 ? 0 : run.Passives.DodgeWindowBonus; } }
        private void OnPassivePerfectDodge()
        { if (run.Companions.Count > 0 && run.Passives.Has(PassiveKind.Nimble)) { dashCooldown = 0; PassiveCue(PassiveKind.Nimble, "기민한 척후", hero.pos + Vector2.up * 2.8f); } }

        // Heals: the healer's regular pulse, overflow into shields and the moon herb's lingering regeneration.
        private void CompanionHeal(CompanionState state)
        {
            var passive = state.Definition.Passive;
            if (hero.hp >= PlayerMaxHealth && !run.Passives.Has(PassiveKind.Overheal)) return;
            float amount = state.HealAmount;
            if (passive.Kind == PassiveKind.Lifewater)
            { float scale = TowerPassiveState.LifewaterScale(passive, 1 - hero.hp / PlayerMaxHealth); if (scale > 1.05f) Fired(passive.Kind); amount *= scale; }
            HealPlayer(amount, true);
            if (passive.Kind == PassiveKind.MoonHerb) StartHerb(passive);
        }
        private void StartHerb(CompanionPassive passive) { herb = passive; herbRemaining = passive.Duration; Fired(passive.Kind); }
        private void OverhealShield(float excess)
        {
            if (excess <= 0 || overflowClaimed || run.Companions.Count == 0) return;
            var passive = run.Passives.Active(PassiveKind.Overheal); if (passive == null) return;
            float cap = PlayerMaxHealth * passive.Secondary; if (playerShield >= cap) return;
            playerShield = Mathf.Min(cap, playerShield + excess); shieldTime = Mathf.Max(shieldTime, passive.Duration); Fired(PassiveKind.Overheal);
        }
        private void OnPassiveSkillCast(HeroState owner, SkillState skill, Fighter source, Fighter target, float scale)
        {
            var passives = run.Passives;
            if (run.Companions.Count == 0) return;
            if (passives.Owns(owner, PassiveKind.SkillRelay))
            {
                var relay = TowerPassiveCatalog.For(owner.Id); bool shortened = false;
                foreach (var clock in skillClocks.Values)
                    if (clock.owner != owner && clock.remaining > 0) { clock.remaining = Mathf.Max(0, clock.remaining - relay.Value); shortened = true; }
                if (shortened) { Fired(PassiveKind.SkillRelay); if (vfx != null) vfx.ImpactRing(hero.pos + Vector2.up, 1.2f, CompanionTint((CompanionState)owner)); }
            }
            if (scale >= 1 && passives.Owns(owner, PassiveKind.Creation))
            {
                var creation = TowerPassiveCatalog.For(owner.Id);
                if (passiveRandom.NextDouble() < creation.Value) skillEchoes.Add(new SkillEcho { owner = owner, skill = skill, source = source, target = target, delay = .3f, scale = creation.Secondary });
            }
            if (passives.Owns(owner, PassiveKind.MoonHerb) && skill.Definition.Effect == SkillEffect.HealPulse) StartHerb(TowerPassiveCatalog.For(owner.Id));
        }
    }
}
