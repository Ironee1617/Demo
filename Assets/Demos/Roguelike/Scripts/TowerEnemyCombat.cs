using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private enum EnemyPattern { MeleeSlam, AimedFan, GapWave, RadialGap }
        private sealed class EnemyState
        {
            public int ordinal, cycle, releases;
            public float clock, hoverClock, phase, hoverHeight, hoverX, lastRelease = -1, followUp = -1;
            public EnemyPattern pattern;
            public Transform shadow;
        }
        private readonly Dictionary<Fighter, EnemyState> enemyCombat = new Dictionary<Fighter, EnemyState>();

        private void ConfigureEnemy(Fighter actor, int kind, int ordinal)
        {
            actor.kind = kind;
            actor.isBoss = false;
            actor.airborne = kind == 3;
            actor.difficultyFloor = run == null ? 1 : run.Floor;
            actor.damageScale = TowerDifficulty.DamageMultiplier(actor.difficultyFloor);
            actor.moveScale = TowerDifficulty.MoveMultiplier(actor.difficultyFloor);
            actor.bulletSpeedScale = TowerDifficulty.BulletMultiplier(actor.difficultyFloor);
            actor.attackRateScale = TowerDifficulty.AttackRateMultiplier(actor.difficultyFloor);
            actor.hp = actor.maxHp = (kind == 3 ? 52 : kind == 2 ? 80 : kind == 1 ? 48 : 60) * TowerDifficulty.HealthMultiplier(actor.difficultyFloor);
            ApplyMonsterStats(actor);
            actor.cooldown = (.9f + ordinal * .27f) / actor.attackRateScale;
            actor.windup = actor.attackPose = actor.slowTime = actor.stunTime = 0;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            actor.velocityY = 0; actor.grounded = !actor.airborne;
            if (actor.airborne && actor.pos.y <= TowerWorld.Ground) actor.pos.y = TowerWorld.Ground + 3.8f;
            var state = CreateEnemyState(actor, ordinal);
            enemyCombat[actor] = state;
            if (actor.airborne) actor.pos.y = state.hoverHeight + Mathf.Sin(state.phase) * .4f;
            actor.root.transform.position = actor.pos;
            UpdateEnemyShadow(actor, state);
        }

        private static void ApplyMonsterStats(Fighter actor)
        {
            var definition = actor.monster;
            if (definition == null) return;
            actor.hp = actor.maxHp *= definition.HealthMultiplier;
            actor.damageScale *= definition.DamageMultiplier;
            actor.moveScale *= definition.MoveMultiplier;
            actor.bulletSpeedScale *= definition.BulletMultiplier;
            actor.attackRateScale *= definition.AttackRateMultiplier;
        }

        private static Color ThemedBoltColor(Fighter actor, Color fallback)
        {
            Color color;
            return actor.monster != null && ColorUtility.TryParseHtmlString("#" + TowerThemeCatalog.Get(actor.theme).AttackHex, out color) ? color : fallback;
        }

        private static EnemyState CreateEnemyState(Fighter actor, int ordinal)
        {
            ordinal = Mathf.Max(0, ordinal);
            return new EnemyState { ordinal = ordinal, cycle = ordinal % 3, phase = ordinal * 1.37f,
                hoverHeight = actor.pos.y, hoverX = actor.pos.x, shadow = actor.root.transform.Find("Shadow"),
                pattern = actor.kind == 0 ? EnemyPattern.MeleeSlam : actor.kind == 2 ? EnemyPattern.RadialGap : (EnemyPattern)(1 + ordinal % 3) };
        }

        private void ResetEnemyCombat()
        {
            enemyCombat.Clear();
            ResetBossCombat();
        }

        private void RemoveEnemyCombat(Fighter actor)
        {
            RemoveBossCombat(actor);
            enemyCombat.Remove(actor);
        }

        internal string EnemyPatternName(Fighter actor)
        { if (actor != null && actor.isBoss) return BossPatternName(actor); EnemyState state; return enemyCombat.TryGetValue(actor, out state) ? state.pattern.ToString() : "None"; }
        internal float EnemyTelegraphRemaining(Fighter actor)
        { return actor == null ? 0 : Mathf.Max(0, actor.windup); }
        internal Vector2 EnemyLockedAim(Fighter actor)
        { return actor == null ? Vector2.zero : actor.strikePoint; }
        internal float EnemyLastReleaseAt(Fighter actor)
        { if (actor != null && actor.isBoss) return BossLastReleaseAt(actor); EnemyState state; return enemyCombat.TryGetValue(actor, out state) ? state.lastRelease : -1; }
        internal int EnemyReleaseCount(Fighter actor)
        { if (actor != null && actor.isBoss) return BossReleaseCount(actor); EnemyState state; return enemyCombat.TryGetValue(actor, out state) ? state.releases : 0; }

        private void TickEnemy(Fighter actor, float dt)
        {
            if (actor == null) return;
            if (actor.isBoss) { TickBoss(actor, dt); return; }
            if (actor.hp <= 0 || !actor.root) { RemoveEnemyCombat(actor); return; }
            EnemyState state;
            // Fixture-created actors may already carry custom health/cooldowns. Lazy state
            // registration must not reset their gameplay stats or revive a dead actor.
            if (!enemyCombat.TryGetValue(actor, out state)) { state = CreateEnemyState(actor, enemies.IndexOf(actor)); enemyCombat.Add(actor, state); }
            state.clock += dt;
            actor.hurt = Mathf.Max(0, actor.hurt - dt);
            actor.attackPose = Mathf.Max(0, actor.attackPose - dt);
            actor.cooldown = Mathf.Max(0, actor.cooldown - dt);
            actor.slowTime = Mathf.Max(0, actor.slowTime - dt);
            actor.stunTime = Mathf.Max(0, actor.stunTime - dt);
            float beforeX = actor.pos.x;
            bool moving = false;
            if (actor.stunTime > 0)
            {
                CancelEnemyWindup(actor, state);
            }
            else if (actor.windup > 0)
            {
                // strikePoint and facing were captured at the beginning of the windup.
                // Neither target tracking nor homing is applied during or after the windup.
                actor.windup = Mathf.Max(0, actor.windup - dt);
                if (actor.windup <= 0)
                {
                    if (EnemyCanFire(actor)) ReleaseEnemyPattern(actor, state, 0);
                    else actor.cooldown = Mathf.Max(actor.cooldown, .8f);
                }
            }
            else if (state.followUp >= 0)
            {
                state.followUp -= dt;
                if (state.followUp <= 0)
                {
                    state.followUp = -1;
                    if (EnemyCanFire(actor)) ReleaseEnemyPattern(actor, state, 1);
                }
            }
            else
            {
                Vector2 delta = hero.pos - actor.pos;
                float distance = delta.magnitude;
                actor.facing = delta.x >= 0 ? 1 : -1;
                if (distance < 15)
                {
                    bool melee = actor.kind == 0 || actor.kind == 2 && state.cycle % 3 == 0;
                    float approach = melee ? actor.kind == 2 ? 1.75f : 1.4f : actor.airborne ? 6.8f : 6.1f;
                    float speed = (actor.kind == 2 ? 1.35f : actor.airborne ? 1.75f : 1.9f) * actor.moveScale * (actor.slowTime > 0 ? .45f : 1);
                    if (Mathf.Abs(delta.x) > approach + .3f)
                    { actor.pos.x += Mathf.Sign(delta.x) * speed * dt; moving = true; }
                    else if (!melee && Mathf.Abs(delta.x) < 3.6f && actor.cooldown > .7f)
                    { actor.pos.x -= Mathf.Sign(delta.x) * speed * .65f * dt; moving = true; }
                    bool landed = actor.airborne || actor.kind != 3 || actor.grounded;
                    bool inReach = melee ? distance < 2.35f && Mathf.Abs(delta.y) < 1.8f : distance < 11.8f;
                    if (inReach && landed && actor.cooldown <= 0 && EnemyCanFire(actor)) BeginEnemyWindup(actor, state, melee);
                }
            }

            actor.pos.x = Mathf.Clamp(actor.pos.x, TowerWorld.Left + .8f, TowerWorld.Right - .8f);
            if (actor.airborne)
            {
                float old = actor.pos.y;
                if (actor.windup <= 0 && state.followUp < 0) state.hoverClock += dt;
                actor.pos.x = Mathf.Clamp(actor.pos.x, state.hoverX - 1, state.hoverX + 1);
                actor.pos.y = state.hoverHeight + Mathf.Sin(state.hoverClock * 1.35f + state.phase) * .4f;
                actor.velocityY = dt > 0 ? (actor.pos.y - old) / dt : 0; actor.grounded = false;
            }
            else StepGravity(actor, dt);
            actor.root.transform.position = actor.pos;
            UpdateEnemyShadow(actor, state);
            float actualSpeed = dt > 0 ? (actor.pos.x - beforeX) / dt : 0;
            moving = Mathf.Abs(actualSpeed) > .08f;
            actor.art.SetLocomotion(actor.grounded, actor.velocityY, false, Mathf.Abs(actualSpeed), actualSpeed);
            actor.art.SetPose(moving, actor.windup > 0 || actor.attackPose > 0, actor.hurt > 0, actor.facing, dt);
        }

        private bool EnemyCanFire(Fighter actor)
        {
            if (hero.hp <= 0 || !view || Vector2.Distance(actor.pos, hero.pos) > 12.2f) return false;
            Vector3 visible = view.WorldToViewportPoint(actor.pos + Vector2.up * .8f);
            return visible.z > 0 && visible.x > .025f && visible.x < .975f && visible.y > .025f && visible.y < .975f;
        }

        private void BeginEnemyWindup(Fighter actor, EnemyState state, bool melee)
        {
            state.pattern = melee ? EnemyPattern.MeleeSlam : (EnemyPattern)(1 + state.cycle % 3);
            if (actor.kind == 2 && !melee) state.pattern = state.cycle % 3 == 1 ? EnemyPattern.RadialGap : EnemyPattern.GapWave;
            state.cycle++;
            float windup = melee ? actor.kind == 2 ? 1.05f : .8f : state.pattern == EnemyPattern.RadialGap ? 1.2f : 1.0f;
            actor.windup = windup;
            actor.cooldown = windup + (melee ? 1.1f : 2.0f) / actor.attackRateScale;
            actor.strikePoint = hero.pos;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            state.followUp = -1;
            StartEnemyMotion(actor, windup);
        }

        private void CancelEnemyWindup(Fighter actor, EnemyState state)
        { actor.windup = 0; state.followUp = -1; }

        private void ReleaseEnemyPattern(Fighter actor, EnemyState state, int stage)
        {
            Vector2 origin = actor.pos + Vector2.up;
            Vector2 aim = actor.strikePoint + Vector2.up;
            Vector2 direction = (aim - origin).sqrMagnitude < .001f ? Vector2.right * actor.facing : (aim - origin).normalized;
            state.lastRelease = state.clock; state.releases++;
            actor.attackPose = Mathf.Max(actor.attackPose, .3f);
            Color color = state.pattern == EnemyPattern.AimedFan ? new Color(1, .43f, .26f) : state.pattern == EnemyPattern.GapWave ? new Color(1, .7f, .26f) : new Color(.95f, .4f, .7f);
            if (state.pattern == EnemyPattern.MeleeSlam)
            {
                SlashArc(actor.pos + new Vector2(actor.facing * .8f, .9f), actor.facing, color);
                if (Vector2.Distance(hero.pos, actor.strikePoint) < 1.4f && Vector2.Distance(hero.pos, actor.pos) < 2.35f && Mathf.Abs(hero.pos.y - actor.pos.y) < 1.8f)
                    HurtHero((actor.kind == 2 ? 12 : 10) * actor.damageScale);
            }
            else if (state.pattern == EnemyPattern.AimedFan)
            {
                foreach (float offset in new[] { -18f, 0f, 18f }) EnemyBolt(actor, origin, direction, offset, 4.6f, 9, color);
            }
            else if (state.pattern == EnemyPattern.GapWave)
            {
                float[] angles = stage == 0 ? new[] { -38f, -19f, 19f, 38f } : new[] { -48f, -29f, -10f, 10f, 29f, 48f };
                foreach (float offset in angles) EnemyBolt(actor, origin, direction, offset, 4.2f, 8, color);
                if (stage == 0) state.followUp = .45f;
            }
            else
            {
                float rotation = stage == 0 ? 0 : 15;
                for (int i = 0; i < 12; i++)
                {
                    float offset = i * 30 + rotation;
                    if (Mathf.Abs(Mathf.DeltaAngle(0, offset)) < 35) continue;
                    EnemyBolt(actor, origin, direction, offset, 3.8f, actor.kind == 2 ? 11 : 9, color);
                }
                if (stage == 0) state.followUp = .5f;
            }
            Sound(hitSound, .25f);
        }

        private void EnemyBolt(Fighter actor, Vector2 origin, Vector2 direction, float offset, float speed, float damage, Color color)
        {
            Vector2 velocity = Quaternion.Euler(0, 0, offset) * direction;
            Fire(origin, origin + velocity * 12, speed * actor.bulletSpeedScale, damage * actor.damageScale, true, ThemedBoltColor(actor, color));
            StyleHostileLastShot(.16f);
        }

        private static void UpdateEnemyShadow(Fighter actor, EnemyState state)
        {
            if (!state.shadow) return;
            float height = Mathf.Max(0, actor.pos.y - TowerWorld.Ground);
            state.shadow.localPosition = new Vector3(0, TowerWorld.Ground - actor.pos.y + .025f, 0);
            state.shadow.localScale = new Vector3(actor.airborne ? 1.05f : 1.2f, .13f, 1);
            var renderer = state.shadow.GetComponent<SpriteRenderer>();
            if (renderer) renderer.color = new Color(0, 0, 0, Mathf.Clamp(.34f - height * .035f, .15f, .34f));
        }
    }
}
